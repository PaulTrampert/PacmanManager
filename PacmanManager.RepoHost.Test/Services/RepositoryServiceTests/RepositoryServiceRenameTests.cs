using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.CliTools;
using PacmanManager.RepoHost.Config;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;
using PacmanManager.RepoHost.Startup.LibAlpm;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Services.RepositoryServiceTests;

/// <summary>
/// Renaming a repository: the retirement a rename writes, the hold it puts on the old name, and the
/// lazy release of a name nothing has asked for. A collision with a <em>live</em> name is the unique
/// index's to report, and is covered against Postgres in <see cref="RepositoryServiceCollisionTests"/>.
/// </summary>
[TestFixture]
public class RepositoryServiceRenameTests
{
    private static readonly DateTimeOffset RenamedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Window = RepositoryRenameConfig.DefaultRedirectWindow;
    private static readonly TimeSpan Hold = RepositoryRenameConfig.DefaultHoldWindow;
    private static readonly TimeSpan Resolution = RepositoryRenameConfig.DefaultLastRequestedAtResolution;

    private PacmanManagerDbContext _dbContext = null!;
    private Mock<ICliToolRunner> _cliRunner = null!;
    private TestActorAccessor _actors = null!;
    private TestTimeProvider _clock = null!;
    private RepositoryService _service = null!;
    private User _owner = null!;
    private User _otherUser = null!;

    [SetUp]
    public void SetUp()
    {
        _dbContext = new PacmanManagerDbContext(new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _owner = _dbContext.Add(new User { DisplayName = "owner", NormalizedDisplayName = "owner", Email = "owner@test.com" }).Entity;
        _otherUser = _dbContext.Add(new User { DisplayName = "other", NormalizedDisplayName = "other", Email = "other@test.com" }).Entity;
        _dbContext.SaveChanges();

        _cliRunner = new Mock<ICliToolRunner>();
        _cliRunner.Setup(c => c.RunToolAsync(It.IsAny<RepoAdd>(), It.IsAny<ICliOutputHandler>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _actors = new TestActorAccessor { Actor = Actor.For(_owner, ActorScope.Unrestricted) };
        _clock = new TestTimeProvider(RenamedAt);

        _service = new RepositoryService(
            _dbContext,
            _cliRunner.Object,
            _actors,
            new RepositoryAccessPolicy(),
            new TestOutputLogger<RepositoryService>(),
            new Mock<IFileSystem>().Object,
            new PackagePathResolver(Options.Create(new PacmanConfigSettings { DataDir = "/tmp/pacman" })),
            Options.Create(new RepositoryRenameConfig()),
            _clock);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region What a rename writes

    [Test]
    public async Task UpdateRepositoryAsync_Renaming_RetiresTheOldName()
    {
        var repository = await GivenRepositoryAsync("custom");

        await RenameAsync(repository.Id, "custom2");

        var retired = await RetirementsAsync();
        Assert.That(retired, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(retired[0].Name, Is.EqualTo("custom"));
            Assert.That(retired[0].RepositoryId, Is.EqualTo(repository.Id));
            Assert.That(retired[0].RetiredAt, Is.EqualTo(RenamedAt));
            Assert.That(retired[0].RedirectUntil, Is.EqualTo(RenamedAt + Window));
            Assert.That(retired[0].LastRequestedAt, Is.Null);
            Assert.That(retired[0].LastRequesterId, Is.Null);
        });
    }

    [Test]
    public async Task UpdateRepositoryAsync_KeepingTheName_RetiresNothing()
    {
        var repository = await GivenRepositoryAsync("custom");

        await _service.UpdateRepositoryAsync(repository.Id, new WriteRepositoryRequest { Name = "custom", IsPublic = true });

        Assert.That(await RetirementsAsync(), Is.Empty);
    }

    [Test]
    public async Task UpdateRepositoryAsync_RenamingTwice_RetiresBothOldNames()
    {
        var repository = await GivenRepositoryAsync("first");

        await RenameAsync(repository.Id, "second");
        await RenameAsync(repository.Id, "third");

        Assert.That((await RetirementsAsync()).Select(r => (r.Name, r.RepositoryId)),
            Is.EquivalentTo(new[] { ("first", repository.Id), ("second", repository.Id) }));
    }

    [Test]
    public async Task UpdateRepositoryAsync_ARenameThatIsRefused_RetiresNothing()
    {
        var mine = await GivenRepositoryAsync("mine");
        var theirs = await GivenRepositoryAsync("theirs", owner: _otherUser);
        await RenameAsync(theirs.Id, "theirs-renamed", actor: _otherUser);

        await Assert.ThrowsAsync<ItemExistsException>(() => RenameAsync(mine.Id, "theirs"));

        _dbContext.ChangeTracker.Clear();
        Assert.Multiple(async () =>
        {
            Assert.That((await RetirementsAsync()).Select(r => r.Name), Is.EqualTo(new[] { "theirs" }));
            Assert.That((await _dbContext.PacmanRepositories.SingleAsync(r => r.Id == mine.Id)).Name, Is.EqualTo("mine"));
        });
    }

    #endregion

    #region Claiming a held name

    [Test]
    public async Task CreateRepositoryAsync_Throws_WhenAnotherRepositorysRetirementHoldsTheName()
    {
        var theirs = await GivenRepositoryAsync("custom", owner: _otherUser);
        await RenameAsync(theirs.Id, "custom2", actor: _otherUser);

        var thrown = await Assert.ThrowsAsync<ItemExistsException>(
            () => _service.CreateRepositoryAsync(new WriteRepositoryRequest { Name = "custom" }));

        Assert.Multiple(async () =>
        {
            Assert.That(thrown!.Message, Is.EqualTo("A repository with this name already exists."));
            Assert.That(await _dbContext.PacmanRepositories.CountAsync(r => r.Name == "custom"), Is.Zero);
            _cliRunner.Verify(
                c => c.RunToolAsync(It.IsAny<RepoAdd>(), It.IsAny<ICliOutputHandler>(), It.IsAny<CancellationToken>()),
                Times.Never,
                "a held name writes nothing to disk");
        });
    }

    [Test]
    public async Task CreateRepositoryAsync_Throws_WhenTheCallersOwnRetirementHoldsTheName()
    {
        // Only the repository that retired a name may take it back; a new repository is not that
        // repository, whoever owns it.
        var mine = await GivenRepositoryAsync("custom");
        await RenameAsync(mine.Id, "custom2");

        await Assert.ThrowsAsync<ItemExistsException>(
            () => _service.CreateRepositoryAsync(new WriteRepositoryRequest { Name = "custom" }));
    }

    [Test]
    public async Task UpdateRepositoryAsync_Throws_WhenRenamedIntoAnotherRepositorysRetiredName()
    {
        var theirs = await GivenRepositoryAsync("custom", owner: _otherUser);
        await RenameAsync(theirs.Id, "custom2", actor: _otherUser);
        var mine = await GivenRepositoryAsync("mine");

        var thrown = await Assert.ThrowsAsync<ItemExistsException>(() => RenameAsync(mine.Id, "custom"));

        Assert.That(thrown!.Message, Is.EqualTo("A repository with this name already exists."));
    }

    [Test]
    public async Task UpdateRepositoryAsync_TheRetiringRepository_CanTakeItsNameBack()
    {
        var repository = await GivenRepositoryAsync("custom");
        await RenameAsync(repository.Id, "custom2");

        var result = await RenameAsync(repository.Id, "custom");

        // Taking the name back deletes its retirement, which would otherwise redirect the name to
        // itself, and retires the name being vacated in its place.
        Assert.Multiple(async () =>
        {
            Assert.That(result!.Name, Is.EqualTo("custom"));
            Assert.That((await RetirementsAsync()).Select(r => (r.Name, r.RepositoryId)),
                Is.EqualTo(new[] { ("custom2", repository.Id) }));
        });
    }

    [Test]
    public async Task UpdateRepositoryAsync_TheRetiringRepository_CanTakeItsNameBack_AfterTheRedirectHasLapsed()
    {
        var repository = await GivenRepositoryAsync("custom");
        await RenameAsync(repository.Id, "custom2");
        _clock.Advance(Window + TimeSpan.FromDays(1));
        await _service.GetRetiredRepositoryNameAsync("custom");

        var result = await RenameAsync(repository.Id, "custom");

        Assert.That(result!.Name, Is.EqualTo("custom"));
    }

    #endregion

    #region Release

    [Test]
    public async Task CreateRepositoryAsync_ClaimsANamePastBothWindows_AndTheRetirementIsGone()
    {
        var theirs = await GivenRepositoryAsync("custom", owner: _otherUser);
        await RenameAsync(theirs.Id, "custom2", actor: _otherUser);
        _clock.Advance(Window);

        var created = await _service.CreateRepositoryAsync(new WriteRepositoryRequest { Name = "custom" });

        Assert.Multiple(async () =>
        {
            Assert.That(created.Name, Is.EqualTo("custom"));
            Assert.That(await RetirementsAsync(), Is.Empty);
        });
    }

    [Test]
    public async Task UpdateRepositoryAsync_ClaimsANamePastBothWindows_AndTheRetirementIsReplacedByTheNameVacated()
    {
        var theirs = await GivenRepositoryAsync("custom", owner: _otherUser);
        await RenameAsync(theirs.Id, "custom2", actor: _otherUser);
        var mine = await GivenRepositoryAsync("mine");
        _clock.Advance(Window);

        await RenameAsync(mine.Id, "custom");

        Assert.That((await RetirementsAsync()).Select(r => (r.Name, r.RepositoryId)),
            Is.EqualTo(new[] { ("mine", mine.Id) }));
    }

    [Test]
    public async Task CreateRepositoryAsync_Throws_JustBeforeTheRedirectWindowHasPassed()
    {
        var theirs = await GivenRepositoryAsync("custom", owner: _otherUser);
        await RenameAsync(theirs.Id, "custom2", actor: _otherUser);
        _clock.Advance(Window - TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<ItemExistsException>(
            () => _service.CreateRepositoryAsync(new WriteRepositoryRequest { Name = "custom" }));
    }

    [Test]
    public async Task ARequestInsideTheGonePeriod_PushesTheReleaseOutByTheHoldWindow()
    {
        var theirs = await GivenRepositoryAsync("custom", owner: _otherUser, isPublic: true);
        await RenameAsync(theirs.Id, "custom2", actor: _otherUser);

        // Past the redirect, so the name answers 410, and somebody is still asking for it.
        _clock.Advance(Window + TimeSpan.FromDays(5));
        var requestedAt = _clock.Now;
        var resolution = await _service.GetRetiredRepositoryNameAsync("custom");
        Assert.That(resolution?.IsRedirecting, Is.False);

        _clock.Now = requestedAt + Hold - TimeSpan.FromSeconds(1);
        await Assert.ThrowsAsync<ItemExistsException>(
            () => _service.CreateRepositoryAsync(new WriteRepositoryRequest { Name = "custom" }),
            "still held just inside the hold window");

        _clock.Now = requestedAt + Hold;
        var created = await _service.CreateRepositoryAsync(new WriteRepositoryRequest { Name = "custom" });
        Assert.That(created.Name, Is.EqualTo("custom"));
    }

    [Test]
    public async Task ARequestInsideTheRedirectWindow_HoldsTheNamePastIt()
    {
        var theirs = await GivenRepositoryAsync("custom", owner: _otherUser, isPublic: true);
        await RenameAsync(theirs.Id, "custom2", actor: _otherUser);
        _clock.Advance(Window - TimeSpan.FromDays(1));
        await _service.GetRetiredRepositoryNameAsync("custom");

        _clock.Advance(TimeSpan.FromDays(2));

        await Assert.ThrowsAsync<ItemExistsException>(
            () => _service.CreateRepositoryAsync(new WriteRepositoryRequest { Name = "custom" }));
    }

    [TestCase(false, TestName = "ARequestByAnotherUserForAPrivateRepositorysOldName_DoesNotHoldIt")]
    [TestCase(true, TestName = "AnAnonymousRequestForAPrivateRepositorysOldName_DoesNotHoldIt")]
    public async Task ARequestByACallerWhoCannotSeeTheRepository_DoesNotHoldItsOldName(bool anonymous)
    {
        // The caller cannot see the repository, so the request resolves to nothing and is not
        // evidence that one of its clients is still configured for the name. An anonymous pacman
        // client is challenged, and only the request it repeats with credentials counts.
        var theirs = await GivenRepositoryAsync("custom", owner: _otherUser, isPublic: false);
        await RenameAsync(theirs.Id, "custom2", actor: _otherUser);
        _clock.Advance(Window + TimeSpan.FromDays(1));
        if (anonymous)
        {
            _actors.Actor = Actor.Anonymous;
        }

        var resolution = await _service.GetRetiredRepositoryNameAsync("custom");
        _actors.Actor = Actor.For(_owner, ActorScope.Unrestricted);

        Assert.Multiple(async () =>
        {
            Assert.That(resolution, Is.Null);
            Assert.That((await RetirementsAsync()).Single().LastRequestedAt, Is.Null);
            Assert.That((await _service.CreateRepositoryAsync(new WriteRepositoryRequest { Name = "custom" })).Name,
                Is.EqualTo("custom"));
        });
    }

    #endregion

    #region Looking a retired name up

    [Test]
    public async Task GetRetiredRepositoryNameAsync_ResolvesToTheRepositoryUnderItsCurrentName()
    {
        var repository = await GivenRepositoryAsync("first", isPublic: true);
        await RenameAsync(repository.Id, "second");
        await RenameAsync(repository.Id, "third");

        var resolution = await _service.GetRetiredRepositoryNameAsync("first");

        Assert.Multiple(() =>
        {
            Assert.That(resolution?.Repository.Id, Is.EqualTo(repository.Id));
            Assert.That(resolution?.Repository.Name, Is.EqualTo("third"));
            Assert.That(resolution?.IsRedirecting, Is.True);
        });
    }

    [Test]
    public async Task GetRetiredRepositoryNameAsync_StopsRedirecting_AtTheEndOfTheWindow()
    {
        var repository = await GivenRepositoryAsync("custom", isPublic: true);
        await RenameAsync(repository.Id, "custom2");
        _clock.Advance(Window);

        var resolution = await _service.GetRetiredRepositoryNameAsync("custom");

        Assert.That(resolution?.IsRedirecting, Is.False);
    }

    [TestCase("custom2", TestName = "a live name")]
    [TestCase("never-existed", TestName = "a name nothing ever had")]
    [TestCase("Custom", TestName = "a retired name in the wrong case")]
    public async Task GetRetiredRepositoryNameAsync_ResolvesNothing_ForANameNoRepositoryRetired(string name)
    {
        var repository = await GivenRepositoryAsync("custom", isPublic: true);
        await RenameAsync(repository.Id, "custom2");

        Assert.That(await _service.GetRetiredRepositoryNameAsync(name), Is.Null);
    }

    [Test]
    public async Task GetRetiredRepositoryNameAsync_RecordsTheRequestAndTheRequestingUser()
    {
        var repository = await GivenRepositoryAsync("custom", isPublic: true);
        await RenameAsync(repository.Id, "custom2");
        _clock.Advance(TimeSpan.FromDays(2));
        _actors.Actor = Actor.For(_otherUser, ActorScope.Unrestricted);

        await _service.GetRetiredRepositoryNameAsync("custom");

        var retired = (await RetirementsAsync()).Single();
        Assert.Multiple(() =>
        {
            Assert.That(retired.LastRequestedAt, Is.EqualTo(_clock.Now));
            Assert.That(retired.LastRequesterId, Is.EqualTo(_otherUser.Id));
        });
    }

    [Test]
    public async Task GetRetiredRepositoryNameAsync_RecordsNoRequester_ForAnAnonymousRequest()
    {
        var repository = await GivenRepositoryAsync("custom", isPublic: true);
        await RenameAsync(repository.Id, "custom2");
        _actors.Actor = Actor.Anonymous;

        await _service.GetRetiredRepositoryNameAsync("custom");

        var retired = (await RetirementsAsync()).Single();
        Assert.Multiple(() =>
        {
            Assert.That(retired.LastRequestedAt, Is.EqualTo(RenamedAt));
            Assert.That(retired.LastRequesterId, Is.Null);
        });
    }

    [Test]
    public async Task GetRetiredRepositoryNameAsync_WritesTheRequestCoarsely()
    {
        var repository = await GivenRepositoryAsync("custom", isPublic: true);
        await RenameAsync(repository.Id, "custom2");
        await _service.GetRetiredRepositoryNameAsync("custom");

        _clock.Advance(Resolution - TimeSpan.FromSeconds(1));
        _actors.Actor = Actor.For(_otherUser, ActorScope.Unrestricted);
        await _service.GetRetiredRepositoryNameAsync("custom");
        var withinResolution = (await RetirementsAsync()).Single();

        _clock.Advance(TimeSpan.FromSeconds(1));
        await _service.GetRetiredRepositoryNameAsync("custom");
        var atResolution = (await RetirementsAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(withinResolution.LastRequestedAt, Is.EqualTo(RenamedAt), "not rewritten within the resolution");
            Assert.That(withinResolution.LastRequesterId, Is.EqualTo(_owner.Id));
            Assert.That(atResolution.LastRequestedAt, Is.EqualTo(RenamedAt + Resolution));
            Assert.That(atResolution.LastRequesterId, Is.EqualTo(_otherUser.Id));
        });
    }

    [Test]
    public async Task GetRetiredRepositoryNameAsync_ResolvesAPrivateRepositorysOldName_ForItsOwnerOnly()
    {
        var repository = await GivenRepositoryAsync("custom", isPublic: false);
        await RenameAsync(repository.Id, "custom2");

        var forOwner = await _service.GetRetiredRepositoryNameAsync("custom");
        _actors.Actor = Actor.For(_otherUser, ActorScope.Unrestricted);
        var forOther = await _service.GetRetiredRepositoryNameAsync("custom");
        _actors.Actor = Actor.Anonymous;
        var forAnonymous = await _service.GetRetiredRepositoryNameAsync("custom");

        Assert.Multiple(() =>
        {
            Assert.That(forOwner?.Repository.Name, Is.EqualTo("custom2"));
            Assert.That(forOther, Is.Null);
            Assert.That(forAnonymous, Is.Null);
        });
    }

    #endregion

    #region Helpers

    private async Task<List<RetiredRepositoryName>> RetirementsAsync() =>
        await _dbContext.RetiredRepositoryNames.AsNoTracking().OrderBy(r => r.Name).ToListAsync();

    private async Task<Repository?> RenameAsync(Guid id, string newName, User? actor = null)
    {
        var previous = _actors.Actor;
        if (actor is not null)
        {
            _actors.Actor = Actor.For(actor, ActorScope.Unrestricted);
        }

        try
        {
            var current = await _service.GetRepositoryByIdAsync(id);
            return await _service.UpdateRepositoryAsync(id, new WriteRepositoryRequest
            {
                Name = newName,
                SupportedArchitectures = current!.SupportedArchitectures,
                IsPublic = current.IsPublic,
            });
        }
        finally
        {
            _actors.Actor = previous;
        }
    }

    private async Task<PacmanRepository> GivenRepositoryAsync(string name, User? owner = null, bool isPublic = false)
    {
        var repository = new PacmanRepository
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            SupportedArchitectures = [Architectures.X86_64],
            IsPublic = isPublic,
            Owner = owner ?? _owner,
            CreatedAt = RenamedAt,
            UpdatedAt = RenamedAt
        };

        _dbContext.PacmanRepositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        return repository;
    }

    #endregion
}

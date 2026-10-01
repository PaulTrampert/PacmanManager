using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Services;

[TestFixture]
public class UserManagementServiceTests
{
    private TestActorAccessor _actorAccessor;
    private DbContextOptions<PacmanManagerDbContext> _dbContextOptions;
    private PacmanManagerDbContext _dbContext;
    private UserManagementService _subject;

    [SetUp]
    public void SetUp()
    {
        _actorAccessor = new TestActorAccessor();
        _dbContextOptions = new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PacmanManagerDbContext(_dbContextOptions);
        _subject = new UserManagementService(_actorAccessor, new UserAccessPolicy(), _dbContext);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Test]
    public async Task GetCurrentUserAsync_WithCurrentUser_ReturnsItProjectedToCurrentUser()
    {
        var user = new User { DisplayName = "Alex", NormalizedDisplayName = "alex", Email = "alex@example.com" };
        _actorAccessor.Actor = Actor.For(user, ActorScope.Unrestricted);

        var result = await _subject.GetCurrentUserAsync();

        Assert.That(result, Is.EqualTo(new CurrentUser
        {
            Id = user.Id,
            DisplayName = "Alex",
            Email = "alex@example.com",
        }));
    }

    [Test]
    public async Task GetCurrentUserAsync_WithNoCurrentUser_ThrowsNoCurrentUserException()
    {
        _actorAccessor.Actor = Actor.Anonymous;

        await Assert.ThrowsAsync<NoCurrentUserException>(() => _subject.GetCurrentUserAsync());
    }

    [Test]
    public async Task GetCurrentUserAsync_AsSystemWithNoUser_ThrowsNoCurrentUserException()
    {
        _actorAccessor.Actor = Actor.System;

        await Assert.ThrowsAsync<NoCurrentUserException>(() => _subject.GetCurrentUserAsync());
    }

    [Test]
    public async Task GetCurrentUserAsync_PassesCancellationTokenThrough()
    {
        using var cts = new CancellationTokenSource();
        var user = new User { DisplayName = "Alex", NormalizedDisplayName = "alex", Email = "alex@example.com" };
        var actorAccessor = new Mock<IActorAccessor>(MockBehavior.Strict);
        actorAccessor
            .Setup(a => a.GetActorAsync(cts.Token))
            .ReturnsAsync(Actor.For(user, ActorScope.Unrestricted));
        var subject = new UserManagementService(actorAccessor.Object, new UserAccessPolicy(), _dbContext);

        await subject.GetCurrentUserAsync(cts.Token);

        actorAccessor.Verify(a => a.GetActorAsync(cts.Token), Times.Once);
    }

    [Test]
    public async Task GetUserByIdAsync_WithKnownId_ReturnsThatUserAsPublicUserInfo()
    {
        var user = _dbContext.Users.Add(new User { DisplayName = "Alex", NormalizedDisplayName = "alex", Email = "alex@example.com" }).Entity;
        _dbContext.Users.Add(new User { DisplayName = "Sam", NormalizedDisplayName = "sam", Email = "sam@example.com" });
        await _dbContext.SaveChangesAsync();

        var result = await _subject.GetUserByIdAsync(user.Id);

        Assert.That(result, Is.EqualTo(new PublicUserInfo
        {
            Id = user.Id,
            DisplayName = "Alex",
        }));
    }

    [Test]
    public async Task GetUserByIdAsync_WithUnknownId_ReturnsNull()
    {
        _dbContext.Users.Add(new User { DisplayName = "Alex", NormalizedDisplayName = "alex", Email = "alex@example.com" });
        await _dbContext.SaveChangesAsync();

        var result = await _subject.GetUserByIdAsync(Guid.CreateVersion7());

        Assert.That(result, Is.Null);
    }

    private User GivenUser(string displayName, string? email = null)
    {
        var user = _dbContext.Users.Add(new User
        {
            DisplayName = displayName,
            NormalizedDisplayName = displayName.ToLowerInvariant(),
            Email = email ?? $"{Guid.NewGuid():N}@example.com",
        }).Entity;
        _dbContext.SaveChanges();
        return user;
    }

    [Test]
    public async Task ListUsersAsync_Unsorted_IsAlphabeticalByDisplayNameIgnoringCase()
    {
        var charlie = GivenUser("charlie");
        var alex = GivenUser("Alex");
        var bea = GivenUser("bea");

        var page = await _subject.ListUsersAsync(new PaginationParams());

        Assert.That(page.Results.Select(u => u.Id), Is.EqualTo(new[] { alex.Id, bea.Id, charlie.Id }));
    }

    [Test]
    public async Task ListUsersAsync_SortedByDisplayNameDescending_RunsZToA()
    {
        var alex = GivenUser("Alex");
        var charlie = GivenUser("charlie");
        var bea = GivenUser("Bea");

        var page = await _subject.ListUsersAsync(
            new PaginationParams(),
            sort: new SortOptions<UserSortField>
            {
                SortBy = UserSortField.DisplayName,
                Direction = SortDirection.Descending,
            });

        Assert.That(page.Results.Select(u => u.Id), Is.EqualTo(new[] { charlie.Id, bea.Id, alex.Id }));
    }

    [Test]
    public async Task ListUsersAsync_Pages()
    {
        var users = Enumerable.Range(0, 5).Select(i => GivenUser($"user-{i}")).ToList();

        var page = await _subject.ListUsersAsync(new PaginationParams { Offset = 1, PageSize = 2 });

        Assert.Multiple(() =>
        {
            Assert.That(page.Results.Select(u => u.Id), Is.EqualTo(new[] { users[1].Id, users[2].Id }));
            Assert.That(page.Offset, Is.EqualTo(1));
            Assert.That(page.Total, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task ListUsersAsync_WithDisplayNameContains_ReturnsOnlyMatchingUsers()
    {
        var alex = GivenUser("Alex");
        var alexandra = GivenUser("Alexandra");
        GivenUser("Sam");

        var page = await _subject.ListUsersAsync(
            new PaginationParams(),
            new UserFilter { DisplayNameContains = "alex" });

        Assert.Multiple(() =>
        {
            Assert.That(page.Results.Select(u => u.Id), Is.EqualTo(new[] { alex.Id, alexandra.Id }));
            Assert.That(page.Total, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ListUsersAsync_WithDisplayNameContains_IgnoresCase()
    {
        // Neither side's case matches the other's, so a naive DisplayName.Contains(term) finds nothing
        // under either the in-memory provider or Npgsql.
        var alex = GivenUser("Alex");
        GivenUser("Sam");

        var page = await _subject.ListUsersAsync(
            new PaginationParams(),
            new UserFilter { DisplayNameContains = "aLEX" });

        Assert.That(page.Results.Select(u => u.Id), Is.EqualTo(new[] { alex.Id }));
    }

    [Test]
    public async Task ListUsersAsync_WithNoMatches_ReturnsAnEmptyPage()
    {
        GivenUser("Alex");

        var page = await _subject.ListUsersAsync(
            new PaginationParams(),
            new UserFilter { DisplayNameContains = "zzz" });

        Assert.Multiple(() =>
        {
            Assert.That(page.Results, Is.Empty);
            Assert.That(page.Total, Is.Zero);
        });
    }

    [Test]
    public async Task ListUsersAsync_WhenAnonymous_ListsEveryUser()
    {
        _actorAccessor.Actor = Actor.Anonymous;
        var alex = GivenUser("Alex");
        var sam = GivenUser("Sam");

        var page = await _subject.ListUsersAsync(new PaginationParams());

        Assert.That(page.Results.Select(u => u.Id), Is.EqualTo(new[] { alex.Id, sam.Id }));
    }

    [Test]
    public async Task ListUsersAsync_ProjectsToPublicUserInfo_WithNoEmail()
    {
        var alex = GivenUser("Alex", "alex@example.com");

        var page = await _subject.ListUsersAsync(new PaginationParams());

        var json = JsonSerializer.Serialize(page);
        Assert.Multiple(() =>
        {
            Assert.That(page.Results.Single(), Is.EqualTo(new PublicUserInfo { Id = alex.Id, DisplayName = "Alex" }));
            Assert.That(json, Does.Not.Contain("alex@example.com"));
            Assert.That(json, Does.Not.Contain("Email").IgnoreCase);
        });
    }

    #region UpdateCurrentUserAsync

    [Test]
    public async Task UpdateCurrentUserAsync_ChangesTheCurrentUsersDisplayNameAndReturnsIt()
    {
        var user = GivenUser("Alex", "alex@example.com");
        _actorAccessor.Actor = Actor.For(user, ActorScope.Unrestricted);

        var result = await _subject.UpdateCurrentUserAsync(new WriteUserRequest { DisplayName = "Alexandra" });

        await using var fresh = new PacmanManagerDbContext(_dbContextOptions);
        var stored = await fresh.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new CurrentUser
            {
                Id = user.Id,
                DisplayName = "Alexandra",
                Email = "alex@example.com",
            }));
            Assert.That(stored.DisplayName, Is.EqualTo("Alexandra"));
            Assert.That(stored.NormalizedDisplayName, Is.EqualTo("alexandra"));
            Assert.That(stored.Email, Is.EqualTo("alex@example.com"));
        });
    }

    [Test]
    public async Task UpdateCurrentUserAsync_LeavesOtherUsersUntouched()
    {
        var user = GivenUser("Alex");
        var other = GivenUser("Sam");
        _actorAccessor.Actor = Actor.For(user, ActorScope.Unrestricted);

        await _subject.UpdateCurrentUserAsync(new WriteUserRequest { DisplayName = "Alexandra" });

        await using var fresh = new PacmanManagerDbContext(_dbContextOptions);
        var stored = await fresh.Users.SingleAsync(u => u.Id == other.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.DisplayName, Is.EqualTo("Sam"));
            Assert.That(stored.NormalizedDisplayName, Is.EqualTo("sam"));
        });
    }

    [Test]
    public async Task UpdateCurrentUserAsync_WithAnActorUserThisContextDoesNotTrack_UpdatesTheStoredUser()
    {
        var user = GivenUser("Alex");
        // A copy, as a tool or another context would supply: not the instance this context tracks.
        var untracked = user with { };
        _actorAccessor.Actor = Actor.For(untracked, ActorScope.Unrestricted);

        await _subject.UpdateCurrentUserAsync(new WriteUserRequest { DisplayName = "Alexandra" });

        await using var fresh = new PacmanManagerDbContext(_dbContextOptions);
        var stored = await fresh.Users.SingleAsync(u => u.Id == user.Id);
        Assert.That(stored.DisplayName, Is.EqualTo("Alexandra"));
    }

    [Test]
    public async Task UpdateCurrentUserAsync_NormalizesDisplayNameIndependentlyOfCurrentCulture()
    {
        // Under tr-TR, "I" lowers to the dotless "ı", so culture-sensitive lowering would store "ıris".
        var turkish = CultureInfo.GetCultureInfo("tr-TR");
        Assume.That("Iris".ToLower(turkish), Is.Not.EqualTo("iris"),
            "tr-TR lowers the same as the invariant culture here, so this test cannot tell them apart");

        var user = GivenUser("Alex");
        _actorAccessor.Actor = Actor.For(user, ActorScope.Unrestricted);

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = turkish;

            await _subject.UpdateCurrentUserAsync(new WriteUserRequest { DisplayName = "Iris" });
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        await using var fresh = new PacmanManagerDbContext(_dbContextOptions);
        var stored = await fresh.Users.SingleAsync(u => u.Id == user.Id);
        Assert.That(stored.NormalizedDisplayName, Is.EqualTo("iris"));
    }

    [Test]
    public async Task UpdateCurrentUserAsync_WithNoCurrentUser_ThrowsNoCurrentUserException()
    {
        _actorAccessor.Actor = Actor.Anonymous;

        await Assert.ThrowsAsync<NoCurrentUserException>(
            () => _subject.UpdateCurrentUserAsync(new WriteUserRequest { DisplayName = "Alexandra" }));
    }

    [Test]
    public async Task UpdateCurrentUserAsync_AsSystemWithNoUser_ThrowsNoCurrentUserException()
    {
        GivenUser("Alex");
        _actorAccessor.Actor = Actor.System;

        await Assert.ThrowsAsync<NoCurrentUserException>(
            () => _subject.UpdateCurrentUserAsync(new WriteUserRequest { DisplayName = "Alexandra" }));
    }

    [Test]
    public async Task UpdateCurrentUserAsync_WhenTheCurrentUserIsNotStored_ThrowsNoCurrentUserException()
    {
        var unstored = new User { DisplayName = "Ghost", NormalizedDisplayName = "ghost", Email = "ghost@example.com" };
        _actorAccessor.Actor = Actor.For(unstored, ActorScope.Unrestricted);

        await Assert.ThrowsAsync<NoCurrentUserException>(
            () => _subject.UpdateCurrentUserAsync(new WriteUserRequest { DisplayName = "Alexandra" }));
    }

    #endregion

    #region Scope

    // UserAccessPolicyTests covers every row of the verdicts; these show that each me method asks its
    // own verdict before doing anything, and turns Forbidden into InsufficientScopeException.

    [Test]
    public async Task GetCurrentUserAsync_WhenTheVerdictIsForbidden_ThrowsInsufficientScope()
    {
        var user = GivenUser("Alex");
        _actorAccessor.Actor = ScopedActor(user, "pacman-manager:repositories:*");

        var e = await Assert.ThrowsAsync<InsufficientScopeException>(() => _subject.GetCurrentUserAsync());

        AssertRefused(e, ScopeValues.ActionNames.Read);
    }

    [Test]
    public async Task GetCurrentUserAsync_WithNoneOfOurValues_ThrowsInsufficientScope()
    {
        // Its user is known, so it is refused rather than challenged.
        var user = GivenUser("Alex");
        _actorAccessor.Actor = Actor.For(user, ActorScope.Empty);

        var e = await Assert.ThrowsAsync<InsufficientScopeException>(() => _subject.GetCurrentUserAsync());

        AssertRefused(e, ScopeValues.ActionNames.Read);
    }

    [Test]
    public async Task GetCurrentUserAsync_WhenTheVerdictIsAllowed_Proceeds()
    {
        var user = GivenUser("Alex");
        _actorAccessor.Actor = ScopedActor(user, "pacman-manager:users:read");

        var result = await _subject.GetCurrentUserAsync();

        Assert.That(result.Id, Is.EqualTo(user.Id));
    }

    [Test]
    public async Task UpdateCurrentUserAsync_WhenTheVerdictIsForbidden_ThrowsInsufficientScope_AndChangesNothing()
    {
        var user = GivenUser("Alex");
        _actorAccessor.Actor = ScopedActor(user, "pacman-manager:users:read");

        var e = await Assert.ThrowsAsync<InsufficientScopeException>(
            () => _subject.UpdateCurrentUserAsync(new WriteUserRequest { DisplayName = "Alexandra" }));

        AssertRefused(e, ScopeValues.ActionNames.Update);
        await using var fresh = new PacmanManagerDbContext(_dbContextOptions);
        var stored = await fresh.Users.SingleAsync(u => u.Id == user.Id);
        Assert.That(stored.DisplayName, Is.EqualTo("Alex"), "unchanged");
    }

    [Test]
    public async Task UpdateCurrentUserAsync_WithUpdateButNotRead_ThrowsInsufficientScope()
    {
        var user = GivenUser("Alex");
        _actorAccessor.Actor = ScopedActor(user, "pacman-manager:users:update");

        var e = await Assert.ThrowsAsync<InsufficientScopeException>(
            () => _subject.UpdateCurrentUserAsync(new WriteUserRequest { DisplayName = "Alexandra" }));

        AssertRefused(e, ScopeValues.ActionNames.Update);
    }

    [Test]
    public async Task UpdateCurrentUserAsync_WhenTheVerdictIsAllowed_Proceeds()
    {
        var user = GivenUser("Alex");
        _actorAccessor.Actor = ScopedActor(user, "pacman-manager:users:update pacman-manager:users:read");

        var result = await _subject.UpdateCurrentUserAsync(new WriteUserRequest { DisplayName = "Alexandra" });

        Assert.That(result.DisplayName, Is.EqualTo("Alexandra"));
    }

    [Test]
    public async Task ListUsersAsync_WithoutUsersRead_ListsEveryUser()
    {
        // An anonymous route: a credential that may not read users reads it as a stranger does.
        var alex = GivenUser("Alex");
        var sam = GivenUser("Sam");
        _actorAccessor.Actor = ScopedActor(alex, "pacman-manager:repositories:*");

        var page = await _subject.ListUsersAsync(new PaginationParams());

        Assert.That(page.Results.Select(u => u.Id), Is.EqualTo(new[] { alex.Id, sam.Id }));
    }

    [Test]
    public async Task GetUserByIdAsync_WithoutUsersRead_ReturnsTheUser()
    {
        var alex = GivenUser("Alex");
        _actorAccessor.Actor = ScopedActor(alex, "pacman-manager:repositories:*");

        var result = await _subject.GetUserByIdAsync(alex.Id);

        Assert.That(result?.Id, Is.EqualTo(alex.Id));
    }

    private static Actor ScopedActor(User user, string claim) =>
        Actor.For(user, ActorScope.Parse(claim, NullLogger.Instance));

    private static void AssertRefused(InsufficientScopeException? e, string action)
    {
        Assert.Multiple(() =>
        {
            Assert.That(e!.Entity, Is.EqualTo(ScopeValues.EntityNames.Users));
            Assert.That(e.Action, Is.EqualTo(action));
        });
    }

    #endregion
}

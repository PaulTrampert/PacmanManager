using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.CliTools;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;
using PacmanManager.RepoHost.Startup.LibAlpm;
using PacmanManager.TestUtils;
using NUnit.Framework;
using PacmanManager.RepoHost.Exceptions;

namespace PacmanManager.RepoHost.Test.Services;

[TestFixture]
public class RepositoryServiceTests
{
    private DbContextOptions<PacmanManagerDbContext> _dbContextOptions;
    private Mock<ICliToolRunner> _mockCliRunner;
    private Mock<IOptionsSnapshot<PacmanConfigSettings>> _mockPacmanSettings;
    private TestActorAccessor _actors;
    private TestOutputLogger<RepositoryService> _logger;
    private Mock<IFileSystem> _mockFileSystem;
    private PacmanManagerDbContext _dbContext;
    private RepositoryService _service;
    private User _existingUser;
    private User _otherUser;

    [SetUp]
    public void SetUp()
    {
        _dbContextOptions = new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PacmanManagerDbContext(_dbContextOptions);
        _existingUser = _dbContext.Add(new User
        {
            DisplayName = "tester",
            Email = "test@test.com"
        }).Entity;
        _otherUser = _dbContext.Add(new User
        {
            DisplayName = "somebody else",
            Email = "other@test.com"
        }).Entity;
        _dbContext.SaveChanges();

        _mockCliRunner = new Mock<ICliToolRunner>();
        // Most tests care about what the owner of a repository can do, so that is the default
        // actor. Tests that exercise the authorization rules override it.
        _actors = new TestActorAccessor { Actor = Actor.For(_existingUser) };
        _logger = new TestOutputLogger<RepositoryService>();
        _mockFileSystem = new Mock<IFileSystem>();

        var settings = new PacmanConfigSettings
        {
            DataDir = "/tmp/pacman"
        };
        _mockPacmanSettings = new Mock<IOptionsSnapshot<PacmanConfigSettings>>();
        _mockPacmanSettings.Setup(s => s.Value).Returns(settings);

        _service = new RepositoryService(
            _dbContext,
            _mockCliRunner.Object,
            _actors,
            new RepositoryAccessPolicy(),
            _mockPacmanSettings.Object,
            _logger,
            _mockFileSystem.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Test]
    public async Task CreateRepositoryAsync_CreatesRepository_Successfully()
    {
        // Arrange
        var request = new WriteRepositoryRequest { Name = "new-repo", Architecture = "x86_64", IsPublic = true};
        _mockCliRunner.Setup(c => c.RunToolAsync(It.IsAny<RepoAdd>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.CreateRepositoryAsync(request);

        // Assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Name, Is.EqualTo("new-repo"));
            Assert.That(result.Architecture, Is.EqualTo("x86_64"));
            Assert.That(result.IsPublic, Is.True);

            var dbRepo = await _dbContext.PacmanRepositories.SingleAsync(r => r.Name == "new-repo");
            Assert.That(dbRepo, Is.Not.Null);
        });
    }

    [Test]
    public async Task CreateRepositoryAsync_AssignsOwnershipToCurrentUser()
    {
        // Arrange
        var request = new WriteRepositoryRequest { Name = "owned-repo", Architecture = "x86_64" };
        _mockCliRunner.Setup(c => c.RunToolAsync(It.IsAny<RepoAdd>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.CreateRepositoryAsync(request);

        // Assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result.Owner, Is.EqualTo(PublicUserInfo.FromUser(_existingUser)));

            var dbRepo = await _dbContext.PacmanRepositories.SingleAsync(r => r.Name == "owned-repo");
            Assert.That(dbRepo.OwnerId, Is.EqualTo(_existingUser.Id));
        });
    }

    [Test]
    public async Task GetRepositoryByIdAsync_ReturnsRepository_WhenExists()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        var repository = await GivenRepositoryAsync(id: repoId, name: "existing-id-repo", isPublic: true);

        // Act
        var result = await _service.GetRepositoryByIdAsync(repoId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Id, Is.EqualTo(repoId));
            Assert.That(result.Architecture, Is.EqualTo(repository.Architecture));
            Assert.That(result.IsPublic, Is.True);
            Assert.That(result.CreatedAt, Is.EqualTo(repository.CreatedAt));
            Assert.That(result.UpdatedAt, Is.EqualTo(repository.UpdatedAt));
            Assert.That(result.Owner, Is.EqualTo(PublicUserInfo.FromUser(_existingUser)));
        });
    }

    [Test]
    public async Task GetRepositoryByIdAsync_ReturnsNull_WhenRepositoryDoesNotExist()
    {
        // Arrange
        var repoId = Guid.NewGuid();

        // Act
        var result = await _service.GetRepositoryByIdAsync(repoId);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetRepositoryByNameAsync_ReturnsRepository_WhenExists()
    {
        // Arrange
        var repoName = "existing-repo";
        await GivenRepositoryAsync(name: repoName);

        // Act
        var result = await _service.GetRepositoryByNameAsync(KeyFor(repoName));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Name, Is.EqualTo(repoName));
        });
    }

    [Test]
    public async Task GetRepositoryByNameAsync_DistinguishesRepositoriesOfDifferentOwners()
    {
        // Names are unique per owner, so two users can both own "shared-name".
        // Arrange
        var theirs = await GivenRepositoryAsync(name: "shared-name", owner: _otherUser, isPublic: true);
        var mine = await GivenRepositoryAsync(name: "shared-name", owner: _existingUser);

        // Act
        var mineResult = await _service.GetRepositoryByNameAsync(KeyFor("shared-name", owner: _existingUser));
        var theirsResult = await _service.GetRepositoryByNameAsync(KeyFor("shared-name", owner: _otherUser));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(mineResult!.Id, Is.EqualTo(mine.Id));
            Assert.That(theirsResult!.Id, Is.EqualTo(theirs.Id));
        });
    }

    [Test]
    public async Task GetRepositoryByNameAsync_DistinguishesRepositoriesOfDifferentArchitectures()
    {
        // One owner may hold the same name for more than one architecture.
        // Arrange
        var x86 = await GivenRepositoryAsync(name: "multi-arch", architecture: "x86_64");
        var any = await GivenRepositoryAsync(name: "multi-arch", architecture: "any");

        // Act
        var x86Result = await _service.GetRepositoryByNameAsync(KeyFor("multi-arch", architecture: "x86_64"));
        var anyResult = await _service.GetRepositoryByNameAsync(KeyFor("multi-arch", architecture: "any"));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(x86Result!.Id, Is.EqualTo(x86.Id));
            Assert.That(anyResult!.Id, Is.EqualTo(any.Id));
        });
    }

    [Test]
    public async Task GetRepositoryByNameAsync_ReturnsNull_WhenTheNameBelongsToADifferentOwner()
    {
        // Arrange
        await GivenRepositoryAsync(name: "theirs", owner: _otherUser, isPublic: true);

        // Act
        var result = await _service.GetRepositoryByNameAsync(KeyFor("theirs", owner: _existingUser));

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetRepositoryByNameAsync_ReturnsNull_WhenPrivateAndOwnedBySomeoneElse()
    {
        // Naming the owner explicitly must not become a way around the visibility rules.
        // Arrange
        await GivenRepositoryAsync(name: "theirs-private", owner: _otherUser);

        // Act
        var result = await _service.GetRepositoryByNameAsync(KeyFor("theirs-private", owner: _otherUser));

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetRepositoryFileByNameAsync_ReturnsStream_WhenFileExists()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        var repoName = "existing-file-repo";
        await GivenRepositoryAsync(id: repoId, name: repoName);

        var repoFileName = Path.Combine("/tmp/pacman/libalpm", "sync", $"{repoId}.db.tar.gz");
        _mockFileSystem.Setup(f => f.OpenRead(repoFileName)).Returns(new MemoryStream());

        // Act
        var result = await _service.GetRepositoryFileByNameAsync(KeyFor(repoName));

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task GetRepositoryFileByIdAsync_ReturnsStream_WhenFileExists()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        await GivenRepositoryAsync(id: repoId, name: "existing-id-file-repo");

        var repoFileName = Path.Combine("/tmp/pacman/libalpm", "sync", $"{repoId}.db.tar.gz");
        _mockFileSystem.Setup(f => f.OpenRead(repoFileName)).Returns(new MemoryStream());

        // Act
        var result = await _service.GetRepositoryFileByIdAsync(repoId);

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task GetRepositoryFileByNameAsync_ReturnsNull_WhenRepositoryDoesNotExist()
    {
        // Arrange
        var repoName = "non-existent-repo";

        // Act
        var result = await _service.GetRepositoryFileByNameAsync(KeyFor(repoName));

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetRepositoryFileByIdAsync_ReturnsNull_WhenPrivateAndNotOwner()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        await GivenRepositoryAsync(id: repoId, name: "someone-elses", owner: _otherUser);
        var repoFileName = Path.Combine("/tmp/pacman/libalpm", "sync", $"{repoId}.db.tar.gz");
        _mockFileSystem.Setup(f => f.OpenRead(repoFileName)).Returns(new MemoryStream());

        // Act
        var result = await _service.GetRepositoryFileByIdAsync(repoId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            _mockFileSystem.Verify(f => f.OpenRead(It.IsAny<string>()), Times.Never);
        });
    }

    [Test]
    public void CreateRepositoryAsync_RollsBack_OnFailure()
    {
        // Arrange
        var request = new WriteRepositoryRequest { Name = "fail-repo", Architecture = "x86_64" };
        _mockCliRunner.Setup(c => c.RunToolAsync(It.IsAny<RepoAdd>(), It.Is<CancellationToken>(ct => true)))
            .ThrowsAsync(new Exception("Failed to run tool"));

        _mockFileSystem.Setup(f => f.Exists(It.Is<string>(s => s.Contains("/tmp/pacman/libalpm/sync/") && s.EndsWith(".db.tar.gz")))).Returns(true);

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.ThrowsAsync<Exception>(async () => await _service.CreateRepositoryAsync(request));
            _mockFileSystem.Verify(f => f.Delete(It.Is<string>(s => s.Contains("/tmp/pacman/libalpm/sync/") && s.EndsWith(".db.tar.gz"))), Times.Once);
        });
    }

    [Test]
    public void CreateRepositoryAsync_WhenThereIsNoCurrentUser_Fails()
    {
        // Arrange
        _actors.Actor = Actor.Anonymous;
        var request = new WriteRepositoryRequest { Name = "fail-repo", Architecture = "x86_64" };

        // Act & Assert
        Assert.ThrowsAsync<NoCurrentUserException>(async () => await _service.CreateRepositoryAsync(request));
    }

    [Test]
    public void CreateRepositoryAsync_WhenActorIsSystemWithNoUser_Fails()
    {
        // A system actor bypasses authorization, but a repository still needs an owner.
        // Arrange
        _actors.Actor = Actor.System;
        var request = new WriteRepositoryRequest { Name = "ownerless-repo", Architecture = "x86_64" };

        // Act & Assert
        Assert.ThrowsAsync<NoCurrentUserException>(async () => await _service.CreateRepositoryAsync(request));
    }

    [Test]
    public async Task GetRepositoriesAsync_ReturnsEmptyResponse_WhenNoRepositoriesExist()
    {
        // Arrange
        var paginationParams = new PaginationParams { Offset = 0, PageSize = 10 };

        // Act
        var result = await _service.GetRepositoriesAsync(paginationParams, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Results, Is.Empty);
            Assert.That(result.Total, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task GetRepositoriesAsync_ReturnsPaginatedResults_WithFiltering()
    {
        // Arrange
        await GivenRepositoryAsync(name: "repo-a");
        await GivenRepositoryAsync(name: "repo-b", createdAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        await GivenRepositoryAsync(name: "repo-c", createdAt: DateTimeOffset.UtcNow.AddMinutes(-2));

        var paginationParams = new PaginationParams { Offset = 0, PageSize = 10 };
        var filter = new RepositoryFilter { NameContains = "repo-b" };

        // Act
        var result = await _service.GetRepositoriesAsync(paginationParams, filter, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(1));
            Assert.That(ResultCount(result), Is.EqualTo(1));
            Assert.That(result.Results.First().Name, Is.EqualTo("repo-b"));
        });
    }

    [Test]
    public async Task GetRepositoriesAsync_AppliesOffsetAndPageSize()
    {
        // Arrange
        await GivenRepositoryAsync(name: "repo-1");
        await GivenRepositoryAsync(name: "repo-2", createdAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        await GivenRepositoryAsync(name: "repo-3", createdAt: DateTimeOffset.UtcNow.AddMinutes(-2));

        // Order is descending by CreatedAt: repo-1, repo-2, repo-3
        var paginationParams = new PaginationParams { Offset = 1, PageSize = 1 };

        // Act
        var result = await _service.GetRepositoriesAsync(paginationParams, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(3));
            Assert.That(ResultCount(result), Is.EqualTo(1));
            Assert.That(result.Results.First().Name, Is.EqualTo("repo-2"));
        });
    }

    [Test]
    public async Task GetRepositoriesAsync_ReturnsEmpty_WhenOffsetIsAtOrBeyondTotal()
    {
        // Arrange
        await GivenRepositoryAsync(name: "repo-1");

        var paginationParams = new PaginationParams { Offset = 1, PageSize = 10 };

        // Act
        var result = await _service.GetRepositoriesAsync(paginationParams, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Results, Is.Empty);
            Assert.That(result.Total, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetRepositoriesAsync_HandlesEmptyNameFilter()
    {
        // Arrange
        await GivenRepositoryAsync(name: "repo-a");

        var filter = new RepositoryFilter { NameContains = "" };

        // Act
        var result = await _service.GetRepositoriesAsync(new PaginationParams { PageSize = 10 }, filter, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(1));
            Assert.That(result.Results.First().Name, Is.EqualTo("repo-a"));
        });
    }

    [Test]
    public async Task GetRepositoriesAsync_HandlesNullFilter()
    {
        // Arrange
        await GivenRepositoryAsync(name: "repo-a");

        // Act
        var result = await _service.GetRepositoriesAsync(new PaginationParams { PageSize = 10 }, null, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(1));
            Assert.That(result.Results.First().Name, Is.EqualTo("repo-a"));
        });
    }

    [Test]
    public async Task UpdateRepositoryAsync_ReturnsUpdatedRepository_WhenExists()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        await GivenRepositoryAsync(id: repoId, name: "original-name");

        var updateRequest = new WriteRepositoryRequest
        {
            Name = "updated-name",
            Architecture = "arm64",
            IsPublic = true
        };

        // Act
        var result = await _service.UpdateRepositoryAsync(repoId, updateRequest);

        // Assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Name, Is.EqualTo("updated-name"));
            Assert.That(result.Architecture, Is.EqualTo("arm64"));
            Assert.That(result.IsPublic, Is.True);

            var dbRepo = await _dbContext.PacmanRepositories.SingleAsync(r => r.Id == repoId);
            Assert.That(dbRepo.Name, Is.EqualTo("updated-name"));
            Assert.That(dbRepo.Architecture, Is.EqualTo("arm64"));
            Assert.That(dbRepo.IsPublic, Is.True);
        });
    }

    [Test]
    public async Task UpdateRepositoryAsync_ReturnsNull_WhenRepositoryDoesNotExist()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        var updateRequest = new WriteRepositoryRequest
        {
            Name = "non-existent",
            Architecture = "x86_64"
        };

        // Act
        var result = await _service.UpdateRepositoryAsync(repoId, updateRequest);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task DeleteRepositoryAsync_RemovesRepositoryAndFile_WhenOwner()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        await GivenRepositoryAsync(id: repoId, name: "doomed-repo");
        var repoFileName = Path.Combine("/tmp/pacman/libalpm", "sync", $"{repoId}.db.tar.gz");
        _mockFileSystem.Setup(f => f.Exists(repoFileName)).Returns(true);

        // Act
        var result = await _service.DeleteRepositoryAsync(repoId);

        // Assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result, Is.True);
            Assert.That(await _dbContext.PacmanRepositories.AnyAsync(r => r.Id == repoId), Is.False);
            _mockFileSystem.Verify(f => f.Delete(repoFileName), Times.Once);
        });
    }

    [Test]
    public async Task DeleteRepositoryAsync_ReturnsFalse_WhenRepositoryDoesNotExist()
    {
        // Act
        var result = await _service.DeleteRepositoryAsync(Guid.NewGuid());

        // Assert
        Assert.That(result, Is.False);
    }

    #region Authorization

    [Test]
    public async Task GetRepositoryByIdAsync_ReturnsRepository_WhenPublicAndAnonymous()
    {
        // Arrange
        _actors.Actor = Actor.Anonymous;
        var repoId = Guid.NewGuid();
        await GivenRepositoryAsync(id: repoId, name: "public-repo", owner: _otherUser, isPublic: true);

        // Act
        var result = await _service.GetRepositoryByIdAsync(repoId);

        // Assert
        Assert.That(result!.Id, Is.EqualTo(repoId));
    }

    [Test]
    public async Task GetRepositoryByIdAsync_ReturnsNull_WhenPrivateAndAnonymous()
    {
        // Arrange
        _actors.Actor = Actor.Anonymous;
        var repoId = Guid.NewGuid();
        await GivenRepositoryAsync(id: repoId, name: "private-repo", owner: _otherUser);

        // Act
        var result = await _service.GetRepositoryByIdAsync(repoId);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetRepositoryByIdAsync_ReturnsNull_WhenPrivateAndOwnedBySomeoneElse()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        await GivenRepositoryAsync(id: repoId, name: "private-repo", owner: _otherUser);

        // Act
        var result = await _service.GetRepositoryByIdAsync(repoId);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetRepositoryByIdAsync_ReturnsRepository_WhenPrivateAndActorIsSystem()
    {
        // Arrange
        _actors.Actor = Actor.System;
        var repoId = Guid.NewGuid();
        await GivenRepositoryAsync(id: repoId, name: "private-repo", owner: _otherUser);

        // Act
        var result = await _service.GetRepositoryByIdAsync(repoId);

        // Assert
        Assert.That(result!.Id, Is.EqualTo(repoId));
    }

    [Test]
    public async Task GetRepositoriesAsync_AnonymousActor_SeesOnlyPublicRepositories()
    {
        // Arrange
        _actors.Actor = Actor.Anonymous;
        await GivenRepositoryAsync(name: "mine-private", owner: _existingUser);
        await GivenRepositoryAsync(name: "theirs-private", owner: _otherUser);
        await GivenRepositoryAsync(name: "theirs-public", owner: _otherUser, isPublic: true);

        // Act
        var result = await _service.GetRepositoriesAsync(new PaginationParams { PageSize = 50 });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(1));
            Assert.That(result.Results.Single().Name, Is.EqualTo("theirs-public"));
        });
    }

    [Test]
    public async Task GetRepositoriesAsync_AuthenticatedActor_SeesOwnAndPublicRepositories()
    {
        // Arrange
        await GivenRepositoryAsync(name: "mine-private", owner: _existingUser);
        await GivenRepositoryAsync(name: "mine-public", owner: _existingUser, isPublic: true);
        await GivenRepositoryAsync(name: "theirs-private", owner: _otherUser);
        await GivenRepositoryAsync(name: "theirs-public", owner: _otherUser, isPublic: true);

        // Act
        var result = await _service.GetRepositoriesAsync(new PaginationParams { PageSize = 50 });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(3));
            Assert.That(result.Results.Select(r => r.Name),
                Is.EquivalentTo(new[] { "mine-private", "mine-public", "theirs-public" }));
        });
    }

    [Test]
    public async Task GetRepositoriesAsync_PrivateFilter_CannotRevealSomeoneElsesPrivateRepositories()
    {
        // The visibility rule is ANDed onto the caller's criteria, so asking for private
        // repositories can only ever return the caller's own.
        // Arrange
        await GivenRepositoryAsync(name: "mine-private", owner: _existingUser);
        await GivenRepositoryAsync(name: "theirs-private", owner: _otherUser);

        // Act
        var result = await _service.GetRepositoriesAsync(
            new PaginationParams { PageSize = 50 },
            new RepositoryFilter { IsPublic = false });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(1));
            Assert.That(result.Results.Single().Name, Is.EqualTo("mine-private"));
        });
    }

    [Test]
    public async Task GetRepositoriesAsync_PrivateFilter_ReturnsNothingForAnonymousCallers()
    {
        // Arrange
        _actors.Actor = Actor.Anonymous;
        await GivenRepositoryAsync(name: "mine-private", owner: _existingUser);
        await GivenRepositoryAsync(name: "theirs-public", owner: _otherUser, isPublic: true);

        // Act
        var result = await _service.GetRepositoriesAsync(
            new PaginationParams { PageSize = 50 },
            new RepositoryFilter { IsPublic = false });

        // Assert
        Assert.That(result.Total, Is.EqualTo(0));
    }

    [Test]
    public async Task GetRepositoriesAsync_OwnerIdFilter_CannotRevealSomeoneElsesPrivateRepositories()
    {
        // Arrange
        await GivenRepositoryAsync(name: "theirs-private", owner: _otherUser);
        await GivenRepositoryAsync(name: "theirs-public", owner: _otherUser, isPublic: true);

        // Act
        var result = await _service.GetRepositoriesAsync(
            new PaginationParams { PageSize = 50 },
            new RepositoryFilter { OwnerId = _otherUser.Id });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(1));
            Assert.That(result.Results.Single().Name, Is.EqualTo("theirs-public"));
        });
    }

    [Test]
    public async Task GetRepositoriesAsync_FiltersByOwnCallerId_ReturnsOnlyTheCallersRepositories()
    {
        // "Only mine" is just the caller's own id in OwnerId, private repositories included.
        // Arrange
        await GivenRepositoryAsync(name: "mine-private", owner: _existingUser);
        await GivenRepositoryAsync(name: "theirs-public", owner: _otherUser, isPublic: true);

        // Act
        var result = await _service.GetRepositoriesAsync(
            new PaginationParams { PageSize = 50 },
            new RepositoryFilter { OwnerId = _existingUser.Id });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(1));
            Assert.That(result.Results.Single().Name, Is.EqualTo("mine-private"));
        });
    }

    [Test]
    public async Task GetRepositoriesAsync_FiltersByArchitecture()
    {
        // Arrange
        await GivenRepositoryAsync(name: "x86-repo", architecture: "x86_64");
        await GivenRepositoryAsync(name: "any-repo", architecture: "any");

        // Act
        var result = await _service.GetRepositoriesAsync(
            new PaginationParams { PageSize = 50 },
            new RepositoryFilter { Architecture = "any" });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(1));
            Assert.That(result.Results.Single().Name, Is.EqualTo("any-repo"));
        });
    }

    [Test]
    public async Task GetRepositoriesAsync_SortsByName()
    {
        // Arrange
        await GivenRepositoryAsync(name: "charlie");
        await GivenRepositoryAsync(name: "alpha");
        await GivenRepositoryAsync(name: "bravo");

        // Act
        var result = await _service.GetRepositoriesAsync(
            new PaginationParams { PageSize = 50 },
            sort: new SortOptions<RepositorySortField> { SortBy = RepositorySortField.Name, Direction = SortDirection.Ascending });

        // Assert
        Assert.That(result.Results.Select(r => r.Name), Is.EqualTo(new[] { "alpha", "bravo", "charlie" }));
    }

    [Test]
    public async Task GetRepositoriesAsync_SortsByNameDescending()
    {
        // The direction is chosen independently of the property, so the same field sorts both ways.
        // Arrange
        await GivenRepositoryAsync(name: "charlie");
        await GivenRepositoryAsync(name: "alpha");
        await GivenRepositoryAsync(name: "bravo");

        // Act
        var result = await _service.GetRepositoriesAsync(
            new PaginationParams { PageSize = 50 },
            sort: new SortOptions<RepositorySortField> { SortBy = RepositorySortField.Name, Direction = SortDirection.Descending });

        // Assert
        Assert.That(result.Results.Select(r => r.Name), Is.EqualTo(new[] { "charlie", "bravo", "alpha" }));
    }

    [Test]
    public async Task GetRepositoriesAsync_SortsByNameAscending_WhenNoDirectionIsGiven()
    {
        // Name defaults to A→Z rather than to the listing-wide descending default, because which
        // way round is natural belongs to the field.
        // Arrange
        await GivenRepositoryAsync(name: "charlie");
        await GivenRepositoryAsync(name: "alpha");
        await GivenRepositoryAsync(name: "bravo");

        // Act
        var result = await _service.GetRepositoriesAsync(
            new PaginationParams { PageSize = 50 },
            sort: new SortOptions<RepositorySortField> { SortBy = RepositorySortField.Name });

        // Assert
        Assert.That(result.Results.Select(r => r.Name), Is.EqualTo(new[] { "alpha", "bravo", "charlie" }));
    }

    [Test]
    public async Task GetRepositoriesAsync_SortsByNewestCreatedFirst_WhenNoDirectionIsGiven()
    {
        // The dates keep the descending default, so the unsorted listing is unchanged.
        // Arrange
        var now = DateTimeOffset.UtcNow;
        await GivenRepositoryAsync(name: "oldest", createdAt: now.AddDays(-2));
        await GivenRepositoryAsync(name: "newest", createdAt: now);
        await GivenRepositoryAsync(name: "middle", createdAt: now.AddDays(-1));

        // Act
        var result = await _service.GetRepositoriesAsync(
            new PaginationParams { PageSize = 50 },
            sort: new SortOptions<RepositorySortField> { SortBy = RepositorySortField.Created });

        // Assert
        Assert.That(result.Results.Select(r => r.Name), Is.EqualTo(new[] { "newest", "middle", "oldest" }));
    }

    [Test]
    public async Task UpdateRepositoryAsync_ReturnsNull_WhenPrivateAndOwnedBySomeoneElse()
    {
        // A repository the caller cannot see must behave as though it is not there.
        // Arrange
        var repoId = Guid.NewGuid();
        await GivenRepositoryAsync(id: repoId, name: "theirs-private", owner: _otherUser);

        // Act
        var result = await _service.UpdateRepositoryAsync(repoId, new WriteRepositoryRequest { Name = "hijacked" });

        // Assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result, Is.Null);
            var dbRepo = await _dbContext.PacmanRepositories.SingleAsync(r => r.Id == repoId);
            Assert.That(dbRepo.Name, Is.EqualTo("theirs-private"));
        });
    }

    [Test]
    public async Task UpdateRepositoryAsync_Throws_WhenPublicAndOwnedBySomeoneElse()
    {
        // The caller already knows a public repository exists, so refusing outright leaks nothing.
        // Arrange
        var repoId = Guid.NewGuid();
        await GivenRepositoryAsync(id: repoId, name: "theirs-public", owner: _otherUser, isPublic: true);

        // Act & Assert
        Assert.ThrowsAsync<RepositoryForbiddenException>(
            async () => await _service.UpdateRepositoryAsync(repoId, new WriteRepositoryRequest { Name = "hijacked" }));
    }

    [Test]
    public async Task UpdateRepositoryAsync_Throws_WhenPublicAndAnonymous()
    {
        // Arrange
        _actors.Actor = Actor.Anonymous;
        var repoId = Guid.NewGuid();
        await GivenRepositoryAsync(id: repoId, name: "theirs-public", owner: _otherUser, isPublic: true);

        // Act & Assert
        Assert.ThrowsAsync<NoCurrentUserException>(
            async () => await _service.UpdateRepositoryAsync(repoId, new WriteRepositoryRequest { Name = "hijacked" }));
    }

    [Test]
    public async Task UpdateRepositoryAsync_Succeeds_WhenActorIsSystem()
    {
        // Arrange
        _actors.Actor = Actor.System;
        var repoId = Guid.NewGuid();
        await GivenRepositoryAsync(id: repoId, name: "theirs-private", owner: _otherUser);

        // Act
        var result = await _service.UpdateRepositoryAsync(repoId, new WriteRepositoryRequest { Name = "renamed" });

        // Assert
        Assert.That(result!.Name, Is.EqualTo("renamed"));
    }

    [Test]
    public async Task DeleteRepositoryAsync_ReturnsFalse_WhenPrivateAndOwnedBySomeoneElse()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        await GivenRepositoryAsync(id: repoId, name: "theirs-private", owner: _otherUser);

        // Act
        var result = await _service.DeleteRepositoryAsync(repoId);

        // Assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result, Is.False);
            Assert.That(await _dbContext.PacmanRepositories.AnyAsync(r => r.Id == repoId), Is.True);
            _mockFileSystem.Verify(f => f.Delete(It.IsAny<string>()), Times.Never);
        });
    }

    [Test]
    public async Task DeleteRepositoryAsync_Throws_WhenPublicAndOwnedBySomeoneElse()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        await GivenRepositoryAsync(id: repoId, name: "theirs-public", owner: _otherUser, isPublic: true);

        // Act & Assert
        Assert.ThrowsAsync<RepositoryForbiddenException>(async () => await _service.DeleteRepositoryAsync(repoId));
        Assert.That(await _dbContext.PacmanRepositories.AnyAsync(r => r.Id == repoId), Is.True);
    }

    #endregion

    private async Task<PacmanRepository> GivenRepositoryAsync(
        Guid? id = null,
        string name = "a-repo",
        User? owner = null,
        bool isPublic = false,
        string architecture = "x86_64",
        DateTimeOffset? createdAt = null)
    {
        var timestamp = createdAt ?? DateTimeOffset.UtcNow;
        var repository = new PacmanRepository
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Architecture = architecture,
            IsPublic = isPublic,
            Owner = owner ?? _existingUser,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };

        await _dbContext.PacmanRepositories.AddAsync(repository);
        await _dbContext.SaveChangesAsync();
        return repository;
    }

    private RepositoryKey KeyFor(string name, User? owner = null, string architecture = "x86_64") => new()
    {
        OwnerId = (owner ?? _existingUser).Id,
        Name = name,
        Architecture = architecture,
    };

    private static int ResultCount<T>(PaginatedResponse<T> response) => response.Results.Count();

    /// <summary>
    /// An <see cref="IActorAccessor"/> whose actor can be swapped between arrangement and action.
    /// </summary>
    private sealed class TestActorAccessor : IActorAccessor
    {
        public Actor Actor { get; set; } = Actor.Anonymous;

        public ValueTask<Actor> GetActorAsync(CancellationToken ct = default) => ValueTask.FromResult(Actor);
    }
}

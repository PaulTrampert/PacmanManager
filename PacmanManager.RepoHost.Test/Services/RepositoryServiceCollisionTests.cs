using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.CliTools;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;
using PacmanManager.RepoHost.Startup.LibAlpm;
using PacmanManager.RepoHost.Test.Containers;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Repository name collisions. Nothing checks for a collision before writing; the unique index
/// decides, and the in-memory provider does not enforce unique indexes, so these run against
/// Postgres.
/// </summary>
[TestFixture]
public class RepositoryServiceCollisionTests
{
    private DatabaseContainer _database;
    private INetwork _network;
    private DbContextOptions<PacmanManagerDbContext> _dbContextOptions;
    private PacmanManagerDbContext _dbContext;
    private Mock<ICliToolRunner> _mockCliRunner;
    private Mock<IFileSystem> _mockFileSystem;
    private TestActorAccessor _actors;
    private PacmanConfigSettings _settings;
    private RepositoryService _service;
    private User _existingUser;
    private User _otherUser;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _network = new NetworkBuilder()
            .WithName(nameof(RepositoryServiceCollisionTests))
            .WithCleanUp(true)
            .Build();
        _database = new DatabaseContainer(_network);
        await _database.StartAsync(await TestImages.Migrations.GetAsync());
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _database.DisposeAsync();
        await _network.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        _dbContextOptions = new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseNpgsql(_database.LocalConnectionString)
            .Options;

        _dbContext = new PacmanManagerDbContext(_dbContextOptions);
        _existingUser = _dbContext.Add(new User { DisplayName = "tester", Email = "test@test.com" }).Entity;
        _otherUser = _dbContext.Add(new User { DisplayName = "somebody else", Email = "other@test.com" }).Entity;
        await _dbContext.SaveChangesAsync();

        _mockCliRunner = new Mock<ICliToolRunner>();
        _mockCliRunner.Setup(c => c.RunToolAsync(It.IsAny<RepoAdd>(), It.IsAny<ICliOutputHandler>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _mockFileSystem = new Mock<IFileSystem>();
        _actors = new TestActorAccessor { Actor = Actor.For(_existingUser) };
        _settings = new PacmanConfigSettings { DataDir = "/tmp/pacman" };
        var pacmanSettings = new Mock<IOptionsSnapshot<PacmanConfigSettings>>();
        pacmanSettings.Setup(s => s.Value).Returns(_settings);

        _service = new RepositoryService(
            _dbContext,
            _mockCliRunner.Object,
            _actors,
            new RepositoryAccessPolicy(),
            pacmanSettings.Object,
            new TestOutputLogger<RepositoryService>(),
            _mockFileSystem.Object);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.PacmanRepositories.ExecuteDeleteAsync();
        await _dbContext.Users.ExecuteDeleteAsync();
        _dbContext.Dispose();
    }

    [Test]
    public async Task CreateRepositoryAsync_Throws_WhenTheOwnerAlreadyHasTheNameAndArchitecture()
    {
        // Arrange
        await GivenRepositoryAsync(name: "taken", architecture: "x86_64");
        var request = new WriteRepositoryRequest { Name = "taken", Architecture = "x86_64" };

        // Act & Assert
        Assert.ThrowsAsync<ItemExistsException>(async () => await _service.CreateRepositoryAsync(request));

        await using var fresh = new PacmanManagerDbContext(_dbContextOptions);
        Assert.That(await fresh.PacmanRepositories.CountAsync(r => r.Name == "taken"), Is.EqualTo(1));
    }

    [Test]
    public async Task CreateRepositoryAsync_OnACollision_RemovesOnlyTheDatabaseFileItWrote()
    {
        // repo-add runs before the index refuses the row, so the collision is cleaned up like any
        // other failure. The file is named for the new id, so the existing repository's is untouched.
        // Arrange
        var existing = await GivenRepositoryAsync(name: "taken", architecture: "x86_64");
        var existingFile = Path.Combine(_settings.DbPath, "sync", $"{existing.Id}.db.tar.gz");
        _mockFileSystem.Setup(f => f.Exists(It.IsAny<string>())).Returns(true);
        var request = new WriteRepositoryRequest { Name = "taken", Architecture = "x86_64" };

        // Act
        Assert.ThrowsAsync<ItemExistsException>(async () => await _service.CreateRepositoryAsync(request));

        // Assert
        _mockFileSystem.Verify(f => f.Delete(It.IsAny<string>()), Times.Once);
        _mockFileSystem.Verify(f => f.Delete(existingFile), Times.Never);
    }

    [Test]
    public async Task CreateRepositoryAsync_Succeeds_WhenTheNameIsTakenOnlyForAnotherArchitecture()
    {
        // Arrange
        await GivenRepositoryAsync(name: "multi-arch", architecture: "any");
        var request = new WriteRepositoryRequest { Name = "multi-arch", Architecture = "x86_64" };

        // Act
        var result = await _service.CreateRepositoryAsync(request);

        // Assert
        Assert.That(result.Architecture, Is.EqualTo("x86_64"));
    }

    [Test]
    public async Task CreateRepositoryAsync_Succeeds_WhenTheNameIsTakenOnlyByAnotherOwner()
    {
        // Names are unique per owner until the global name index lands, including when the other
        // owner's repository is private.
        // Arrange
        await GivenRepositoryAsync(name: "shared-name", owner: _otherUser);
        var request = new WriteRepositoryRequest { Name = "shared-name", Architecture = "x86_64" };

        // Act
        var result = await _service.CreateRepositoryAsync(request);

        // Assert
        Assert.That(result.Owner, Is.EqualTo(PublicUserInfo.FromUser(_existingUser)));
    }

    [Test]
    public async Task UpdateRepositoryAsync_Throws_WhenRenamedIntoAnotherOfTheOwnersRepositories()
    {
        // Arrange
        await GivenRepositoryAsync(name: "taken", architecture: "x86_64");
        var renamed = await GivenRepositoryAsync(name: "original", architecture: "x86_64");
        var update = new WriteRepositoryRequest { Name = "taken", Architecture = "x86_64" };

        // Act & Assert
        Assert.ThrowsAsync<ItemExistsException>(async () => await _service.UpdateRepositoryAsync(renamed.Id, update));

        await using var fresh = new PacmanManagerDbContext(_dbContextOptions);
        var stored = await fresh.PacmanRepositories.SingleAsync(r => r.Id == renamed.Id);
        Assert.That(stored.Name, Is.EqualTo("original"));
    }

    [Test]
    public async Task UpdateRepositoryAsync_Throws_WhenTheArchitectureChangeCollides()
    {
        // Arrange
        await GivenRepositoryAsync(name: "multi-arch", architecture: "any");
        var changed = await GivenRepositoryAsync(name: "multi-arch", architecture: "x86_64");
        var update = new WriteRepositoryRequest { Name = "multi-arch", Architecture = "any" };

        // Act & Assert
        Assert.ThrowsAsync<ItemExistsException>(async () => await _service.UpdateRepositoryAsync(changed.Id, update));
    }

    [Test]
    public async Task UpdateRepositoryAsync_KeepingItsOwnNameAndArchitecture_IsNotACollision()
    {
        // Arrange
        var repository = await GivenRepositoryAsync(name: "unchanged", architecture: "x86_64");
        var update = new WriteRepositoryRequest { Name = "unchanged", Architecture = "x86_64", IsPublic = true };

        // Act
        var result = await _service.UpdateRepositoryAsync(repository.Id, update);

        // Assert
        Assert.That(result!.IsPublic, Is.True);
    }

    [Test]
    public async Task UpdateRepositoryAsync_Succeeds_WhenTheNameIsTakenOnlyByAnotherOwner()
    {
        // Arrange
        await GivenRepositoryAsync(name: "theirs", owner: _otherUser);
        var mine = await GivenRepositoryAsync(name: "mine");
        var update = new WriteRepositoryRequest { Name = "theirs", Architecture = "x86_64" };

        // Act
        var result = await _service.UpdateRepositoryAsync(mine.Id, update);

        // Assert
        Assert.That(result!.Name, Is.EqualTo("theirs"));
    }

    [Test]
    public async Task UpdateRepositoryAsync_Throws_WhenASystemActorRenamesIntoTheOwnersOtherRepository()
    {
        // A system actor writes on the owner's behalf, so the owner's names are what collide.
        // Arrange
        _actors.Actor = Actor.System;
        await GivenRepositoryAsync(name: "taken");
        var renamed = await GivenRepositoryAsync(name: "original");
        var update = new WriteRepositoryRequest { Name = "taken", Architecture = "x86_64" };

        // Act & Assert
        Assert.ThrowsAsync<ItemExistsException>(async () => await _service.UpdateRepositoryAsync(renamed.Id, update));
    }

    private async Task<PacmanRepository> GivenRepositoryAsync(
        string name = "a-repo",
        User? owner = null,
        string architecture = "x86_64")
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new PacmanRepository
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Architecture = architecture,
            IsPublic = false,
            Owner = owner ?? _existingUser,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _dbContext.PacmanRepositories.AddAsync(repository);
        await _dbContext.SaveChangesAsync();
        return repository;
    }
}

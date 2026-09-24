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

namespace PacmanManager.RepoHost.Test.Services.RepositoryServiceTests;

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
    private PackagePathResolver _pathResolver;
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
        _existingUser = _dbContext.Add(new User { DisplayName = "tester", NormalizedDisplayName = "tester", Email = "test@test.com" }).Entity;
        _otherUser = _dbContext.Add(new User { DisplayName = "somebody else", NormalizedDisplayName = "somebody else", Email = "other@test.com" }).Entity;
        await _dbContext.SaveChangesAsync();

        _mockCliRunner = new Mock<ICliToolRunner>();
        _mockCliRunner.Setup(c => c.RunToolAsync(It.IsAny<RepoAdd>(), It.IsAny<ICliOutputHandler>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _mockFileSystem = new Mock<IFileSystem>();
        _actors = new TestActorAccessor { Actor = Actor.For(_existingUser, ActorScope.Unrestricted) };
        _settings = new PacmanConfigSettings { DataDir = "/tmp/pacman" };
        _pathResolver = new PackagePathResolver(Options.Create(_settings));

        _service = new RepositoryService(
            _dbContext,
            _mockCliRunner.Object,
            _actors,
            new RepositoryAccessPolicy(),
            new TestOutputLogger<RepositoryService>(),
            _mockFileSystem.Object,
            _pathResolver);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.PacmanRepositories.ExecuteDeleteAsync();
        await _dbContext.Users.ExecuteDeleteAsync();
        _dbContext.Dispose();
    }

    [Test]
    public async Task CreateRepositoryAsync_Throws_WhenTheOwnerAlreadyHasTheName()
    {
        // Arrange
        await GivenRepositoryAsync(name: "taken");
        var request = new WriteRepositoryRequest { Name = "taken" };

        // Act & Assert
        Assert.ThrowsAsync<ItemExistsException>(async () => await _service.CreateRepositoryAsync(request));

        await using var fresh = new PacmanManagerDbContext(_dbContextOptions);
        Assert.That(await fresh.PacmanRepositories.CountAsync(r => r.Name == "taken"), Is.EqualTo(1));
    }

    [Test]
    public async Task CreateRepositoryAsync_OnACollision_RemovesOnlyTheDirectoryItWrote()
    {
        // repo-add runs before the index refuses the row, so the collision is cleaned up like any
        // other failure. The directory is named for the new id, so the existing repository's is
        // untouched.
        // Arrange
        var existing = await GivenRepositoryAsync(name: "taken");
        var existingDirectory = _pathResolver.GetRepositoryDirectory(existing.Id);
        _mockFileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        var request = new WriteRepositoryRequest { Name = "taken" };

        // Act
        Assert.ThrowsAsync<ItemExistsException>(async () => await _service.CreateRepositoryAsync(request));

        // Assert
        _mockFileSystem.Verify(f => f.DeleteDirectory(It.IsAny<string>()), Times.Once);
        _mockFileSystem.Verify(f => f.DeleteDirectory(existingDirectory), Times.Never);
        _mockFileSystem.Verify(f => f.Delete(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task CreateRepositoryAsync_Throws_WhenTheNameIsTakenBySupportingOtherArchitectures()
    {
        // Architecture is not part of the name: a name belongs to one repository.
        // Arrange
        await GivenRepositoryAsync(name: "multi-arch", architectures: ["aarch64"]);
        var request = new WriteRepositoryRequest { Name = "multi-arch" };

        // Act & Assert
        Assert.ThrowsAsync<ItemExistsException>(async () => await _service.CreateRepositoryAsync(request));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task CreateRepositoryAsync_Throws_WhenAnotherOwnerHoldsTheName(bool theirsIsPublic)
    {
        // Names are unique across the whole deployment, so the collision is reported even when the
        // caller cannot see the repository that holds the name.
        // Arrange
        await GivenRepositoryAsync(name: "custom", owner: _otherUser, isPublic: theirsIsPublic);
        var request = new WriteRepositoryRequest { Name = "custom" };

        // Act & Assert
        Assert.ThrowsAsync<ItemExistsException>(async () => await _service.CreateRepositoryAsync(request));

        await using var fresh = new PacmanManagerDbContext(_dbContextOptions);
        var holders = await fresh.PacmanRepositories.Where(r => r.Name == "custom").ToListAsync();
        Assert.That(holders.Select(r => r.OwnerId), Is.EqualTo(new[] { _otherUser.Id }));
    }

    [Test]
    public async Task UpdateRepositoryAsync_Throws_WhenRenamedIntoAnotherOfTheOwnersRepositories()
    {
        // Arrange
        await GivenRepositoryAsync(name: "taken");
        var renamed = await GivenRepositoryAsync(name: "original");
        var update = new WriteRepositoryRequest { Name = "taken" };

        // Act & Assert
        Assert.ThrowsAsync<ItemExistsException>(async () => await _service.UpdateRepositoryAsync(renamed.Id, update));

        await using var fresh = new PacmanManagerDbContext(_dbContextOptions);
        var stored = await fresh.PacmanRepositories.SingleAsync(r => r.Id == renamed.Id);
        Assert.That(stored.Name, Is.EqualTo("original"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task UpdateRepositoryAsync_Throws_WhenRenamedIntoANameAnotherOwnerHolds(bool theirsIsPublic)
    {
        // Arrange
        await GivenRepositoryAsync(name: "theirs", owner: _otherUser, isPublic: theirsIsPublic);
        var mine = await GivenRepositoryAsync(name: "mine");
        var update = new WriteRepositoryRequest { Name = "theirs" };

        // Act & Assert
        Assert.ThrowsAsync<ItemExistsException>(async () => await _service.UpdateRepositoryAsync(mine.Id, update));

        await using var fresh = new PacmanManagerDbContext(_dbContextOptions);
        var stored = await fresh.PacmanRepositories.SingleAsync(r => r.Id == mine.Id);
        Assert.That(stored.Name, Is.EqualTo("mine"));
    }

    [Test]
    public async Task UpdateRepositoryAsync_KeepingItsOwnName_IsNotACollision()
    {
        // Arrange
        var repository = await GivenRepositoryAsync(name: "unchanged");
        var update = new WriteRepositoryRequest { Name = "unchanged", IsPublic = true };

        // Act
        var result = await _service.UpdateRepositoryAsync(repository.Id, update);

        // Assert
        Assert.That(result!.IsPublic, Is.True);
    }

    [Test]
    public async Task UpdateRepositoryAsync_ChangingOnlyTheArchitectures_IsNotACollision()
    {
        // Arrange
        var repository = await GivenRepositoryAsync(name: "unchanged");
        var update = new WriteRepositoryRequest { Name = "unchanged", SupportedArchitectures = [Architectures.X86_64, "aarch64"] };

        // Act
        var result = await _service.UpdateRepositoryAsync(repository.Id, update);

        // Assert
        Assert.That(result!.SupportedArchitectures, Is.EqualTo(new[] { Architectures.X86_64, "aarch64" }));
    }

    [Test]
    public async Task UpdateRepositoryAsync_Throws_WhenASystemActorRenamesIntoTheOwnersOtherRepository()
    {
        // A system actor bypasses the visibility rules, but not the index.
        // Arrange
        _actors.Actor = Actor.System;
        await GivenRepositoryAsync(name: "taken");
        var renamed = await GivenRepositoryAsync(name: "original");
        var update = new WriteRepositoryRequest { Name = "taken" };

        // Act & Assert
        Assert.ThrowsAsync<ItemExistsException>(async () => await _service.UpdateRepositoryAsync(renamed.Id, update));
    }

    private async Task<PacmanRepository> GivenRepositoryAsync(
        string name = "a-repo",
        User? owner = null,
        IEnumerable<string>? architectures = null,
        bool isPublic = false)
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new PacmanRepository
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            SupportedArchitectures = architectures?.ToList() ?? [Architectures.X86_64],
            IsPublic = isPublic,
            Owner = owner ?? _existingUser,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _dbContext.PacmanRepositories.AddAsync(repository);
        await _dbContext.SaveChangesAsync();
        return repository;
    }
}

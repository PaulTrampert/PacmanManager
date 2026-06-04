using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.RepoHost.CliTools;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;
using PacmanManager.RepoHost.Startup.LibAlpm;
using PacmanManager.TestUtils;
using NUnit.Framework;

namespace PacmanManager.RepoHost.Test.Services;

[TestFixture]

public class RepositoryServiceTests
{
    private DbContextOptions<PacmanManagerDbContext> _dbContextOptions;
    private Mock<ICliToolRunner> _mockCliRunner;
    private Mock<IOptionsSnapshot<PacmanConfigSettings>> _mockPacmanSettings;
    private Mock<ILogger<RepositoryService>> _mockLogger;
    private Mock<IFileSystem> _mockFileSystem;
    private PacmanManagerDbContext _dbContext;
    private RepositoryService _service;

    [SetUp]
    public void SetUp()
    {
        _dbContextOptions = new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PacmanManagerDbContext(_dbContextOptions);

        _mockCliRunner = new Mock<ICliToolRunner>();
        _mockLogger = new Mock<ILogger<RepositoryService>>();
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
            _mockPacmanSettings.Object,
            _mockLogger.Object,
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
        var request = new WriteRepositoryRequest { Name = "new-repo", Architecture = "x86_64" };
        _mockCliRunner.Setup(c => c.RunToolAsync(It.IsAny<RepoAdd>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.CreateRepositoryAsync(request);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo("new-repo"));
            Assert.That(result.Architecture, Is.EqualTo("x86_64"));
            
            var dbRepo = _dbContext.PacmanRepositories.SingleAsync(r => r.Name == "new-repo").GetAwaiter().GetResult();
            Assert.That(dbRepo, Is.Not.Null);
        });
    }

    [Test]
    public async Task GetRepositoryByNameAsync_ReturnsRepository_WhenExists()
    {
        // Arrange
        var repoName = "existing-repo";
        var repository = new PacmanRepository 
        { 
            Id = Guid.NewGuid(), 
            Name = repoName, 
            Architecture = "x86_64", 
            CreatedAt = DateTimeOffset.UtcNow, 
            UpdatedAt = DateTimeOffset.UtcNow 
        };
        await _dbContext.PacmanRepositories.AddAsync(repository);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetRepositoryByNameAsync(repoName);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Name, Is.EqualTo(repoName));
        });
    }

    [Test]
    public async Task GetRepositoryByNameAsync_ReturnsNull_WhenRepositoryDoesNotExist()
    {
        // Arrange
        var repoName = "non-existent-repo";

        // Act
        var result = await _service.GetRepositoryByNameAsync(repoName);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetRepositoryFileByNameAsync_ReturnsStream_WhenFileExists()
    {
        // Arrange
        var repoName = "existing-file-repo";
        var repository = new PacmanRepository 
        { 
            Id = Guid.NewGuid(), 
            Name = repoName, 
            Architecture = "x86_64", 
            CreatedAt = DateTimeOffset.UtcNow, 
            UpdatedAt = DateTimeOffset.UtcNow 
        };
        await _dbContext.PacmanRepositories.AddAsync(repository);
        await _dbContext.SaveChangesAsync();

        var repoFileName = Path.Combine("/tmp/pacman", "libalpm", "sync", $"{repoName}.db.tar.gz");
        _mockFileSystem.Setup(f => f.OpenRead(repoFileName)).Returns(new MemoryStream());

        // Act
        var result = await _service.GetRepositoryFileByNameAsync(repoName);

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task GetRepositoryFileByNameAsync_ReturnsNull_WhenRepositoryDoesNotExist()
    {
        // Arrange
        var repoName = "non-existent-repo";

        // Act
        var result = await _service.GetRepositoryFileByNameAsync(repoName);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task CreateRepositoryAsync_RollsBack_OnFailure()

    {
        // Arrange
        var request = new WriteRepositoryRequest { Name = "fail-repo", Architecture = "x86_64" };
        _mockCliRunner.Setup(c => c.RunToolAsync(It.IsAny<RepoAdd>(), It.Is<CancellationToken>(ct => true)))
            .ThrowsAsync(new Exception("Failed to run tool"));

        var expectedRepoPath = Path.Combine("/tmp/pacman/libalpm", "sync", "fail-repo.db.tar.gz");
        _mockFileSystem.Setup(f => f.Exists(expectedRepoPath)).Returns(true);

        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.ThrowsAsync<Exception>(async () => await _service.CreateRepositoryAsync(request));
            _mockFileSystem.Verify(f => f.Delete(expectedRepoPath), Times.Once);
        });
    }

}

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
    private TestOutputLogger<RepositoryService> _logger;
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
        var request = new WriteRepositoryRequest { Name = "new-repo", Architecture = "x86_64" };
        _mockCliRunner.Setup(c => c.RunToolAsync(It.IsAny<RepoAdd>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.CreateRepositoryAsync("abc123", request);

        // Assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Name, Is.EqualTo("new-repo"));
            Assert.That(result.Architecture, Is.EqualTo("x86_64"));
            
            var dbRepo = await _dbContext.PacmanRepositories.SingleAsync(r => r.Name == "new-repo");
            Assert.That(dbRepo, Is.Not.Null);
        });
    }

    [Test]
    public async Task GetRepositoryByIdAsync_ReturnsRepository_WhenExists()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        var repository = new PacmanRepository 
        { 
            Id = repoId, 
            OwnerId = "unknown",
            Name = "existing-id-repo", 
            Architecture = "x86_64", 
            CreatedAt = DateTimeOffset.UtcNow, 
            UpdatedAt = DateTimeOffset.UtcNow 
        };
        await _dbContext.PacmanRepositories.AddAsync(repository);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetRepositoryByIdAsync(repoId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Id, Is.EqualTo(repoId));
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
        var repository = new PacmanRepository 
        { 
            Id = Guid.NewGuid(), 
            OwnerId = "unknown",
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
    public async Task GetRepositoryFileByNameAsync_ReturnsStream_WhenFileExists()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        var repoName = "existing-file-repo";
        var repository = new PacmanRepository 
        { 
            Id = repoId, 
            OwnerId = "unknown",
            Name = repoName, 
            Architecture = "x86_64", 
            CreatedAt = DateTimeOffset.UtcNow, 
            UpdatedAt = DateTimeOffset.UtcNow 
        };
        await _dbContext.PacmanRepositories.AddAsync(repository);
        await _dbContext.SaveChangesAsync();

        var repoFileName = Path.Combine("/tmp/pacman/libalpm", "sync", $"{repoId}.db.tar.gz");
        _mockFileSystem.Setup(f => f.OpenRead(repoFileName)).Returns(new MemoryStream());

        // Act
        var result = await _service.GetRepositoryFileByNameAsync(repoName);

        // Assert
        Assert.That(result, Is.Not.Null);
    }


    [Test]
    public async Task GetRepositoryFileByIdAsync_ReturnsStream_WhenFileExists()
    {
        // Arrange
        var repoId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var repository = new PacmanRepository
        {
            Id = repoId,
            OwnerId = "unknown",
            Name = "existing-id-file-repo",
            Architecture = "x86_64",
            CreatedAt = now,
            UpdatedAt = now
        };
        await _dbContext.PacmanRepositories.AddAsync(repository);
        await _dbContext.SaveChangesAsync();

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

        _mockFileSystem.Setup(f => f.Exists(It.Is<string>(s => s.Contains("/tmp/pacman/libalpm/sync/") && s.EndsWith(".db.tar.gz")))).Returns(true);
 
        // Act & Assert
        Assert.Multiple(() =>
        {
            Assert.ThrowsAsync<Exception>(async () => await _service.CreateRepositoryAsync("abc123", request));
            _mockFileSystem.Verify(f => f.Delete(It.Is<string>(s => s.Contains("/tmp/pacman/libalpm/sync/") && s.EndsWith(".db.tar.gz"))), Times.Once);
        });
    }



    [Test]
    public async Task GetRepositoriesAsync_ReturnsEmptyResponse_WhenNoRepositoriesExist()
    {
        // Arrange
        var paginationParams = new PaginationParams { Offset = 0, PageSize = 10 };

        // Act
        var result = await _service.GetRepositoriesAsync(paginationParams, CancellationToken.None);

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
        var repo1 = new PacmanRepository { Id = Guid.NewGuid(), OwnerId = "unknown", Name = "repo-a", Architecture = "x86_64", CreatedAt = DateTimeOffset.UtcNow };
        var repo2 = new PacmanRepository { Id = Guid.NewGuid(), OwnerId = "unknown", Name = "repo-b", Architecture = "x86_64", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        var repo3 = new PacmanRepository { Id = Guid.NewGuid(), OwnerId = "unknown", Name = "repo-c", Architecture = "x86_64", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2) };
        
        await _dbContext.PacmanRepositories.AddRangeAsync(repo1, repo2, repo3);
        await _dbContext.SaveChangesAsync();

        var paginationParams = new PaginationParams { SearchTerm = "repo-b", Offset = 0, PageSize = 10 };

        // Act
        var result = await _service.GetRepositoriesAsync(paginationParams, CancellationToken.None);

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
        var repo1 = new PacmanRepository { Id = Guid.NewGuid(), OwnerId = "unknown", Name = "repo-1", Architecture = "x86_64", CreatedAt = DateTimeOffset.UtcNow };
        var repo2 = new PacmanRepository { Id = Guid.NewGuid(), OwnerId = "unknown", Name = "repo-2", Architecture = "x86_64", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        var repo3 = new PacmanRepository { Id = Guid.NewGuid(), OwnerId = "unknown", Name = "repo-3", Architecture = "x86_64", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2) };
        
        await _dbContext.PacmanRepositories.AddRangeAsync(repo1, repo2, repo3);
        await _dbContext.SaveChangesAsync();

        // Order is descending by CreatedAt: repo-1, repo-2, repo-3
        var paginationParams = new PaginationParams { Offset = 1, PageSize = 1 };

        // Act
        var result = await _service.GetRepositoriesAsync(paginationParams, CancellationToken.None);

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
        var repo1 = new PacmanRepository { Id = Guid.NewGuid(), OwnerId = "unknown", Name = "repo-1", Architecture = "x86_64", CreatedAt = DateTimeOffset.UtcNow };
        await _dbContext.PacmanRepositories.AddAsync(repo1);
        await _dbContext.SaveChangesAsync();

        var paginationParams = new PaginationParams { Offset = 1, PageSize = 10 };

        // Act
        var result = await _service.GetRepositoriesAsync(paginationParams, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Results, Is.Empty);
            Assert.That(result.Total, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetRepositoriesAsync_HandlesEmptySearchTerm()
    {
        // Arrange
        var repo1 = new PacmanRepository { Id = Guid.NewGuid(), OwnerId = "unknown", Name = "repo-a", Architecture = "x86_64", CreatedAt = DateTimeOffset.UtcNow };
        await _dbContext.PacmanRepositories.AddAsync(repo1);
        await _dbContext.SaveChangesAsync();

        var paginationParams = new PaginationParams { SearchTerm = "", Offset = 0, PageSize = 10 };

        // Act
        var result = await _service.GetRepositoriesAsync(paginationParams, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(1));
            Assert.That(result.Results.First().Name, Is.EqualTo("repo-a"));
        });
    }

    [Test]
    public async Task GetRepositoriesAsync_HandlesNullSearchTerm()
    {
        // Arrange
        var repo1 = new PacmanRepository { Id = Guid.NewGuid(), OwnerId = "unknown", Name = "repo-a", Architecture = "x86_64", CreatedAt = DateTimeOffset.UtcNow };
        await _dbContext.PacmanRepositories.AddAsync(repo1);
        await _dbContext.SaveChangesAsync();

        var paginationParams = new PaginationParams { SearchTerm = null, Offset = 0, PageSize = 10 };

        // Act
        var result = await _service.GetRepositoriesAsync(paginationParams, CancellationToken.None);

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
        var repository = new PacmanRepository
        {
            Id = repoId,
            Name = "original-name",
            OwnerId = "unknown", 
            Architecture = "x86_64",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _dbContext.PacmanRepositories.AddAsync(repository);
        await _dbContext.SaveChangesAsync();

        var updateRequest = new WriteRepositoryRequest
        {
            Name = "updated-name",
            Architecture = "arm64"
        };

        // Act
        var result = await _service.UpdateRepositoryAsync(repoId, updateRequest);

        // Assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Name, Is.EqualTo("updated-name"));
            Assert.That(result.Architecture, Is.EqualTo("arm64"));

            var dbRepo = await _dbContext.PacmanRepositories.SingleAsync(r => r.Id == repoId);
            Assert.That(dbRepo.Name, Is.EqualTo("updated-name"));
            Assert.That(dbRepo.Architecture, Is.EqualTo("arm64"));
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

    private int ResultCount<T>(PaginatedResponse<T> response) => response.Results.Count();
}

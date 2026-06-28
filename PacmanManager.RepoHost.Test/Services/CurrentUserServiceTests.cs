using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Services;
using PacmanManager.RepoHost.Test.Containers;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Services;

public class CurrentUserServiceTests
{
    private INetwork _network;
    private DatabaseContainer _database;
    
    private Mock<IHttpContextAccessor> _mockContext;

    private PacmanManagerDbContext _dbContext;

    private TestOutputLogger<CurrentUserService> _logger;

    private CurrentUserService _subject;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _network = new NetworkBuilder()
            .WithName(nameof(CurrentUserServiceTests))
            .Build();
        _database = new DatabaseContainer(_network);
        var solutionDirectory = DirUtils.FindSolutionDirectory();
        var migrationsImage = new ImageFromDockerfileBuilder()
                .WithContextDirectory(solutionDirectory)
                .WithDockerfileDirectory(Path.Combine(solutionDirectory, "PacmanManager.Migrations"))
                .WithName("pacmanmanager-migrations-test:latest")
                .Build();
        await migrationsImage.CreateAsync();
        await _database.StartAsync(migrationsImage);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _database.DisposeAsync();
        await _network.DisposeAsync();
    }

    [SetUp]
    public void Setup()
    {
    }
}
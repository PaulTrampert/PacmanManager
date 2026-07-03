using System.Security.Claims;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
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

    private User _existingUser;

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

        var existingUser = new User
        {
            Email = "test@example.com",
            DisplayName = "Test User"
        };
        
        var optionsBuilder = new DbContextOptionsBuilder<PacmanManagerDbContext>();
        optionsBuilder.UseNpgsql(_database.LocalConnectionString);
        await using var dbContext = new PacmanManagerDbContext(optionsBuilder.Options);

        _existingUser = (await dbContext.Users.AddAsync(existingUser)).Entity;
        await dbContext.SaveChangesAsync();
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
        var optionsBuilder = new DbContextOptionsBuilder<PacmanManagerDbContext>();
        optionsBuilder.UseNpgsql(_database.LocalConnectionString);
        _dbContext = new PacmanManagerDbContext(optionsBuilder.Options);

        _mockContext = new Mock<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        _mockContext.Setup(x => x.HttpContext).Returns(httpContext);

        _logger = new TestOutputLogger<CurrentUserService>();
        _subject = new CurrentUserService(_mockContext.Object, _dbContext, _logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
    }

    [Test]
    public async Task GetCurrentUserAsync_WhenNoUserIdClaim_ReturnsNull()
    {
        var result = await _subject.GetCurrentUserAsync();
        
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetCurrentUserAsync_WhenNoUser_LogsThatThereIsNoUserIdClaim()
    {
        await _subject.GetCurrentUserAsync();

        var logEvent = _logger.LogEvents.Single(l => l.Message == "No userIdClaim found.");
        Assert.That(logEvent.LogLevel, Is.EqualTo(LogLevel.Warning));
    }
    
    [Test]
    public async Task GetCurrentUserAsync_WhenUserIdClaimCannotBeParsed_ReturnsNull()
    {
        _mockContext.SetupGet(c => c.HttpContext)
            .Returns(new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthnConstants.AppUserIdClaimType, "abc")]))
            });
        
        var result = await _subject.GetCurrentUserAsync();
        
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetCurrentUserAsync_WhenUserIdClaimCannotBeParsed_LogsThatThereIsNoUserIdClaim()
    {
        _mockContext.SetupGet(c => c.HttpContext)
            .Returns(new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthnConstants.AppUserIdClaimType, "abc")]))
            });
        
        await _subject.GetCurrentUserAsync();

        var logEvent = _logger.LogEvents.Single(l => l.Message == "Could not parse user id from 'abc'");
        
        Assert.That(logEvent.LogLevel, Is.EqualTo(LogLevel.Warning));
    }
    
    [Test]
    public async Task GetCurrentUserAsync_WhenUserIdClaimExists_ReturnsExistingUser()
    {
        _mockContext.SetupGet(c => c.HttpContext)
            .Returns(new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthnConstants.AppUserIdClaimType, _existingUser.Id.ToString())]))
            });
        
        var result = await _subject.GetCurrentUserAsync();
        
        Assert.That(result, Is.EqualTo(_existingUser));
    }

    [Test]
    public async Task GetCurrentUserAsync_WhenUserIdClaimExists_NothingIsLogged()
    {
        _mockContext.SetupGet(c => c.HttpContext)
            .Returns(new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthnConstants.AppUserIdClaimType, _existingUser.Id.ToString())]))
            });
        
        await _subject.GetCurrentUserAsync();

        Assert.That(_logger.LogEvents.Any(), Is.False);
    }
}
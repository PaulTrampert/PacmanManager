using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Services;
using PacmanManager.RepoHost.Test.Containers;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Services;

[TestFixture]
public class UserServiceTests
{
    private DatabaseContainer _database;
    private INetwork _network;
    private DbContextOptions<PacmanManagerDbContext> _dbContextOptions;
    private PacmanManagerDbContext _dbContext;
    private UserService _service;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _network = new NetworkBuilder()
            .WithName(nameof(UserServiceTests))
            .WithCleanUp(true)
            .Build();
        _database = new DatabaseContainer(_network);
        var migrationsImage = new ImageFromDockerfileBuilder()
            .WithContextDirectory(DirUtils.FindSolutionDirectory())
            .WithDockerfileDirectory(Path.Combine(DirUtils.FindSolutionDirectory(), "PacmanManager.Migrations"))
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
    public void SetUp()
    {
        _dbContextOptions = new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseNpgsql(_database.LocalConnectionString)
            .Options;

        _dbContext = new PacmanManagerDbContext(_dbContextOptions);
        _service = new UserService(_dbContext);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.UserMappings.ExecuteDeleteAsync();
        await _dbContext.Users.ExecuteDeleteAsync();
        _dbContext.Dispose();
    }

    [Test]
    public async Task GetUserByExternalIdAsync_ReturnsUser_WhenMappingExists()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", DisplayName = "Test User" };
        await _dbContext.Users.AddAsync(user);
        await _dbContext.UserMappings.AddAsync(new ExternalProviderUserMapping
        {
            ExternalAuthority = "auth-provider",
            ExternalId = "subject-123",
            User = user
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUserByExternalIdAsync("auth-provider", "subject-123");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(user.Id));
    }

    [Test]
    public async Task GetUserByExternalIdAsync_ReturnsNull_WhenNoMappingExists()
    {
        // Act
        var result = await _service.GetUserByExternalIdAsync("non-existent", "subject");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetUserByEmailAsync_ReturnsUser_WhenExists()
    {
        // Arrange
        var email = "test@example.com";
        var user = new User { Id = Guid.NewGuid(), Email = email, DisplayName = "Test User" };
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetUserByEmailAsync(email);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(user.Id));
    }

    [Test]
    public async Task GetUserByEmailAsync_ReturnsNull_WhenDoesNotExist()
    {
        // Act
        var result = await _service.GetUserByEmailAsync("notfound@example.com");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task CreateUserAsync_ReturnsCreatedUser()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "new@example.com", DisplayName = "New User" };

        // Act
        var result = await _service.CreateUserAsync(user);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Email, Is.EqualTo("new@example.com"));
        Assert.That(await _dbContext.Users.FindAsync(user.Id), Is.Not.Null);
    }

    [Test]
    public async Task LinkToIdentityAsync_CreatesMappingAndReturnsUser()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", DisplayName = "Test User" };
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();
        var authority = "auth-provider";
        var subject = "subject-123";

        // Act
        var result = await _service.LinkToIdentityAsync(user, authority, subject);

        // Assert
        Assert.That(result, Is.SameAs(user));
        var mapping = await _dbContext.UserMappings.FirstOrDefaultAsync(m => m.ExternalAuthority == authority && m.ExternalId == subject);
        Assert.That(mapping, Is.Not.Null);
        Assert.That(mapping!.User.Id, Is.EqualTo(user.Id));
    }

    [Test]
    public async Task EnsureUserLinkedAsync_ExistingUserAndAlreadyLinked_ReturnsExistingUserWithoutChanges()
    {
        // Arrange
        var email = "existing@example.com";
        var user = new User { Id = Guid.NewGuid(), Email = email, DisplayName = "Existing User" };
        await _dbContext.Users.AddAsync(user);
        await _dbContext.UserMappings.AddAsync(new ExternalProviderUserMapping
        {
            ExternalAuthority = "auth",
            ExternalId = "sub",
            User = user
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.EnsureUserLinkedAsync(email, "Existing User", "auth", "sub");

        // Assert
        Assert.That(result.Id, Is.EqualTo(user.Id));
        Assert.That(_dbContext.UserMappings.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task EnsureUserLinkedAsync_ExistingUserNotLinked_LinksSuccessfully()
    {
        // Arrange
        var email = "existing@example.com";
        var user = new User { Id = Guid.NewGuid(), Email = email, DisplayName = "Existing User" };
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.EnsureUserLinkedAsync(email, "Existing User", "auth", "sub");

        // Assert
        Assert.That(result.Id, Is.EqualTo(user.Id));
        var mapping = await _dbContext.UserMappings.FirstOrDefaultAsync(m => m.ExternalAuthority == "auth" && m.ExternalId == "sub");
        Assert.That(mapping, Is.Not.Null);
    }

    [Test]
    public async Task EnsureUserLinkedAsync_NewUser_CreatesAndLinksSuccessfully()
    {
        // Arrange
        var email = "new@example.com";
        var displayName = "New User";

        // Act
        var result = await _service.EnsureUserLinkedAsync(email, displayName, "auth", "sub");

        // Assert
        Assert.That(result.Email, Is.EqualTo(email));
        Assert.That(result.DisplayName, Is.EqualTo(displayName));
        var mapping = await _dbContext.UserMappings.FirstOrDefaultAsync(m => m.ExternalAuthority == "auth" && m.ExternalId == "sub");
        Assert.That(mapping, Is.Not.Null);
        Assert.That(mapping!.User.Email, Is.EqualTo(email));
    }

    [Test]
    public void EnsureUserLinkedAsync_OnException_RollsBackTransaction()
    {
        Assert.ThrowsAsync<DbUpdateException>(async () => 
             await _service.EnsureUserLinkedAsync($"{new string('a', 1000)}@example.com", "Fail", "auth", "sub")); 
    }
}

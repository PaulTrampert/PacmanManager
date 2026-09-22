using Microsoft.EntityFrameworkCore;
using Moq;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Services;

[TestFixture]
public class UserManagementServiceTests
{
    private Mock<ICurrentUserService> _currentUserService;
    private PacmanManagerDbContext _dbContext;
    private UserManagementService _subject;

    [SetUp]
    public void SetUp()
    {
        _currentUserService = new Mock<ICurrentUserService>(MockBehavior.Strict);
        _dbContext = new PacmanManagerDbContext(new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);
        _subject = new UserManagementService(_currentUserService.Object, _dbContext);
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
        _currentUserService
            .Setup(s => s.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _subject.GetCurrentUserAsync();

        Assert.That(result, Is.EqualTo(new CurrentUser
        {
            Id = user.Id,
            DisplayName = "Alex",
            Email = "alex@example.com",
        }));
    }

    [Test]
    public void GetCurrentUserAsync_WithNoCurrentUser_ThrowsNoCurrentUserException()
    {
        _currentUserService
            .Setup(s => s.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        Assert.ThrowsAsync<NoCurrentUserException>(() => _subject.GetCurrentUserAsync());
    }

    [Test]
    public async Task GetCurrentUserAsync_PassesCancellationTokenThrough()
    {
        using var cts = new CancellationTokenSource();
        var user = new User { DisplayName = "Alex", NormalizedDisplayName = "alex", Email = "alex@example.com" };
        _currentUserService
            .Setup(s => s.GetCurrentUserAsync(cts.Token))
            .ReturnsAsync(user);

        await _subject.GetCurrentUserAsync(cts.Token);

        _currentUserService.Verify(s => s.GetCurrentUserAsync(cts.Token), Times.Once);
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
}

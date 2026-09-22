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
    private UserManagementService _subject;

    [SetUp]
    public void SetUp()
    {
        _currentUserService = new Mock<ICurrentUserService>(MockBehavior.Strict);
        _subject = new UserManagementService(_currentUserService.Object);
    }

    [Test]
    public async Task GetCurrentUserAsync_WithCurrentUser_ReturnsItProjectedToCurrentUser()
    {
        var user = new User { DisplayName = "Paul", Email = "paul@example.com" };
        _currentUserService
            .Setup(s => s.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _subject.GetCurrentUserAsync();

        Assert.That(result, Is.EqualTo(new CurrentUser
        {
            Id = user.Id,
            DisplayName = "Paul",
            Email = "paul@example.com",
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
        var user = new User { DisplayName = "Paul", Email = "paul@example.com" };
        _currentUserService
            .Setup(s => s.GetCurrentUserAsync(cts.Token))
            .ReturnsAsync(user);

        await _subject.GetCurrentUserAsync(cts.Token);

        _currentUserService.Verify(s => s.GetCurrentUserAsync(cts.Token), Times.Once);
    }
}

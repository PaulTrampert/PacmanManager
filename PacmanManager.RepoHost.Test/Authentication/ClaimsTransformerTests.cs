using System.Security.Claims;
using Moq;
using NUnit.Framework;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Authentication;

[TestFixture]
public class ClaimsTransformerTests
{
    private Mock<IUserService> _mockUserService;
    private ClaimsTransformer _transformer;

    [SetUp]
    public void SetUp()
    {
        _mockUserService = new Mock<IUserService>();
        _transformer = new ClaimsTransformer(_mockUserService.Object);
    }

    [Test]
    public async Task TransformAsync_ReturnsPrincipal_WhenAppUserIdClaimAlreadyExists()
    {
        // Arrange
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(AuthnConstants.AppUserIdClaimType, "existing-user-id"));
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformer.TransformAsync(principal);

        // Assert
        Assert.That(result.HasClaim(c => c.Type == AuthnConstants.AppUserIdClaimType && c.Value == "existing-user-id"), Is.True);
        _mockUserService.Verify(s => s.GetUserByExternalIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task TransformAsync_ReturnsPrincipal_WhenRequiredClaimsAreMissing()
    {
        // Arrange
        var identity = new ClaimsIdentity();
        // Missing sub, iss, or email
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformer.TransformAsync(principal);

        // Assert
        Assert.That(result, Is.EqualTo(principal));
        _mockUserService.Verify(s => s.GetUserByExternalIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task TransformAsync_AddsAppUserIdClaim_WhenUserExistsExternally()
    {
        // Arrange
        var authority = "https://auth.example.com";
        var subject = "sub123";
        var email = "user@example.com";
        var name = "John Doe";
        var userId = Guid.NewGuid();

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(AuthnConstants.SubClaimType, subject));
        identity.AddClaim(new Claim(AuthnConstants.AuthorityClaimType, authority));
        identity.AddClaim(new Claim(AuthnConstants.EmailClaimType, email));
        identity.AddClaim(new Claim(AuthnConstants.DisplayNameClaimType, name));
        var principal = new ClaimsPrincipal(identity);

        var user = new User { Id = userId, Email = "user@example.com", DisplayName = "John Doe" };

        _mockUserService.Setup(s => s.GetUserByExternalIdAsync(authority, subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _transformer.TransformAsync(principal);

        // Assert
        Assert.That(result.HasClaim(c => c.Type == AuthnConstants.AppUserIdClaimType && c.Value == userId.ToString()), Is.True);
    }

    [Test]
    public async Task TransformAsync_EnsuresUserLinked_WhenUserDoesNotExistExternally()
    {
        // Arrange
        var authority = "https://auth.example.com";
        var subject = "sub123";
        var email = "user@example.com";
        var name = "John Doe";
        var userId = Guid.NewGuid();

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(AuthnConstants.SubClaimType, subject));
        identity.AddClaim(new Claim(AuthnConstants.AuthorityClaimType, authority));
        identity.AddClaim(new Claim(AuthnConstants.EmailClaimType, email));
        identity.AddClaim(new Claim(AuthnConstants.DisplayNameClaimType, name));
        var principal = new ClaimsPrincipal(identity);

        _mockUserService.Setup(s => s.GetUserByExternalIdAsync(authority, subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _mockUserService.Setup(s => s.EnsureUserLinkedAsync(email, name, authority, subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, Email = email, DisplayName = name });

        // Act
        var result = await _transformer.TransformAsync(principal);

        // Assert
        Assert.That(result.HasClaim(c => c.Type == AuthnConstants.AppUserIdClaimType && c.Value == userId.ToString()), Is.True);
        _mockUserService.Verify(s => s.EnsureUserLinkedAsync(email, name, authority, subject, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task TransformAsync_EnsuresUserLinked_WithEmptyName_WhenDisplayNameClaimIsMissing()
    {
        // Arrange
        var authority = "https://auth.example.com";
        var subject = "sub123";
        var email = "user@example.com";
        var userId = Guid.NewGuid();

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(AuthnConstants.SubClaimType, subject));
        identity.AddClaim(new Claim(AuthnConstants.AuthorityClaimType, authority));
        identity.AddClaim(new Claim(AuthnConstants.EmailClaimType, email));
        // DisplayNameClaimType is missing
        var principal = new ClaimsPrincipal(identity);

        _mockUserService.Setup(s => s.GetUserByExternalIdAsync(authority, subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _mockUserService.Setup(s => s.EnsureUserLinkedAsync(email, string.Empty, authority, subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, Email = email, DisplayName = string.Empty });

        // Act
        var result = await _transformer.TransformAsync(principal);

        // Assert
        Assert.That(result.HasClaim(c => c.Type == AuthnConstants.AppUserIdClaimType && c.Value == userId.ToString()), Is.True);
        _mockUserService.Verify(s => s.EnsureUserLinkedAsync(email, string.Empty, authority, subject, It.IsAny<CancellationToken>()), Times.Once);
    }
}

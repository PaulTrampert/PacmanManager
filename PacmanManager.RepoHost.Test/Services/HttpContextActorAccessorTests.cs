using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Services;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Services;

[TestFixture]
public class HttpContextActorAccessorTests
{
    private Mock<IHttpContextAccessor> _httpContextAccessor;
    private PacmanManagerDbContext _dbContext;
    private TestOutputLogger<HttpContextActorAccessor> _logger;
    private HttpContextActorAccessor _subject;
    private User _existingUser;

    [SetUp]
    public async Task SetUp()
    {
        _dbContext = new PacmanManagerDbContext(new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);
        _existingUser = _dbContext.Users.Add(new User { DisplayName = "Alex", Email = "alex@example.com" }).Entity;
        await _dbContext.SaveChangesAsync();

        _httpContextAccessor = new Mock<IHttpContextAccessor>();
        _httpContextAccessor.SetupGet(a => a.HttpContext).Returns(new DefaultHttpContext());

        _logger = new TestOutputLogger<HttpContextActorAccessor>();
        _subject = new HttpContextActorAccessor(_httpContextAccessor.Object, _dbContext, _logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    private void GivenUserIdClaim(string value)
    {
        _httpContextAccessor.SetupGet(a => a.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthnConstants.AppUserIdClaimType, value)])),
        });
    }

    [Test]
    public async Task GetActorAsync_WithValidUserIdClaim_ReturnsActorForThatUser()
    {
        GivenUserIdClaim(_existingUser.Id.ToString());

        var actor = await _subject.GetActorAsync();

        Assert.That(actor, Is.EqualTo(Actor.For(_existingUser)));
    }

    [Test]
    public async Task GetActorAsync_WithValidUserIdClaim_LogsNothing()
    {
        GivenUserIdClaim(_existingUser.Id.ToString());

        await _subject.GetActorAsync();

        Assert.That(_logger.LogEvents, Is.Empty);
    }

    [Test]
    public async Task GetActorAsync_WithNoUserIdClaim_ReturnsAnonymous()
    {
        var actor = await _subject.GetActorAsync();

        Assert.That(actor, Is.SameAs(Actor.Anonymous));
    }

    [Test]
    public async Task GetActorAsync_WithNoUserIdClaim_WarnsThatThereIsNoUserIdClaim()
    {
        await _subject.GetActorAsync();

        var logEvent = _logger.LogEvents.Single(l => l.Message == "No userIdClaim found.");
        Assert.That(logEvent.LogLevel, Is.EqualTo(LogLevel.Warning));
    }

    [Test]
    public async Task GetActorAsync_WithNoHttpContext_ReturnsAnonymous()
    {
        _httpContextAccessor.SetupGet(a => a.HttpContext).Returns((HttpContext?)null);

        var actor = await _subject.GetActorAsync();

        Assert.That(actor, Is.SameAs(Actor.Anonymous));
    }

    [Test]
    public async Task GetActorAsync_WithUnparseableUserIdClaim_ReturnsAnonymous()
    {
        GivenUserIdClaim("abc");

        var actor = await _subject.GetActorAsync();

        Assert.That(actor, Is.SameAs(Actor.Anonymous));
    }

    [Test]
    public async Task GetActorAsync_WithUnparseableUserIdClaim_WarnsThatItCouldNotBeParsed()
    {
        GivenUserIdClaim("abc");

        await _subject.GetActorAsync();

        var logEvent = _logger.LogEvents.Single(l => l.Message == "Could not parse user id from 'abc'");
        Assert.That(logEvent.LogLevel, Is.EqualTo(LogLevel.Warning));
    }

    [Test]
    public async Task GetActorAsync_WithUserIdOfNoUser_ReturnsAnonymous()
    {
        GivenUserIdClaim(Guid.CreateVersion7().ToString());

        var actor = await _subject.GetActorAsync();

        Assert.That(actor, Is.SameAs(Actor.Anonymous));
    }

    [Test]
    public async Task GetActorAsync_CalledTwice_DoesNotQueryAgain()
    {
        GivenUserIdClaim(_existingUser.Id.ToString());
        var first = await _subject.GetActorAsync();

        // Were the second call to query, it would find no user and answer Anonymous.
        _dbContext.Users.Remove(_existingUser);
        await _dbContext.SaveChangesAsync();
        var second = await _subject.GetActorAsync();

        Assert.That(second, Is.SameAs(first));
    }
}

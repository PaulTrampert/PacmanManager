using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        _existingUser = _dbContext.Users.Add(new User { DisplayName = "Alex", NormalizedDisplayName = "alex", Email = "alex@example.com" }).Entity;
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

    private void GivenUserIdClaim(string value, string? scope = null)
    {
        List<Claim> claims = [new Claim(AuthnConstants.AppUserIdClaimType, value)];
        if (scope is not null)
        {
            claims.Add(new Claim(AuthnConstants.ScopeClaimType, scope));
        }

        _httpContextAccessor.SetupGet(a => a.HttpContext).Returns(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims)),
        });
    }

    [Test]
    public async Task GetActorAsync_WithValidUserIdClaim_ReturnsActorForThatUser()
    {
        GivenUserIdClaim(_existingUser.Id.ToString());

        var actor = await _subject.GetActorAsync();

        Assert.That(actor.User, Is.EqualTo(_existingUser));
    }

    [Test]
    public async Task GetActorAsync_WithAScopeClaim_CarriesTheParsedScope()
    {
        GivenUserIdClaim(_existingUser.Id.ToString(), "openid pacman-manager:repositories:read");

        var actor = await _subject.GetActorAsync();

        Assert.That(actor, Is.EqualTo(Actor.For(
            _existingUser, ActorScope.Parse("pacman-manager:repositories:read", NullLogger.Instance))));
    }

    [Test]
    public async Task GetActorAsync_WithEverything_IsUnrestricted()
    {
        GivenUserIdClaim(_existingUser.Id.ToString(), $"openid profile {ScopeValues.Everything}");

        var actor = await _subject.GetActorAsync();

        Assert.That(actor.Scope, Is.EqualTo(ActorScope.Unrestricted));
    }

    [Test]
    public async Task GetActorAsync_WithNoScopeClaim_KeepsTheUserWithAnEmptyScope()
    {
        GivenUserIdClaim(_existingUser.Id.ToString());

        var actor = await _subject.GetActorAsync();

        Assert.Multiple(() =>
        {
            Assert.That(actor.User, Is.EqualTo(_existingUser));
            Assert.That(actor.Scope, Is.EqualTo(ActorScope.Empty));
        });
    }

    [Test]
    public async Task GetActorAsync_WithAScopeCarryingNoneOfOurs_KeepsTheUserButMayOnlyDoWhatAStrangerMay()
    {
        // This pins the direction of the default: a credential with none of our values keeps its
        // user, is refused every change, and reads exactly what an anonymous caller reads.
        GivenUserIdClaim(_existingUser.Id.ToString(), "openid profile email roles pacman-manager");
        var repositoryPolicy = new RepositoryAccessPolicy();
        var packagePolicy = new PackageAccessPolicy(repositoryPolicy);
        var stranger = new User { DisplayName = "s", NormalizedDisplayName = "s", Email = "s@example.com" };
        var repositories = new[]
        {
            RepositoryOf(_existingUser, isPublic: false),
            RepositoryOf(_existingUser, isPublic: true),
            RepositoryOf(stranger, isPublic: false),
            RepositoryOf(stranger, isPublic: true),
        };

        var actor = await _subject.GetActorAsync();

        var ownPublic = RepositoryOf(_existingUser, isPublic: true);
        var ownPrivate = RepositoryOf(_existingUser, isPublic: false);
        Assert.Multiple(() =>
        {
            Assert.That(actor.User, Is.EqualTo(_existingUser));
            Assert.That(actor.Scope, Is.EqualTo(ActorScope.Empty));

            foreach (var repository in new[] { ownPublic, ownPrivate })
            {
                Assert.That(repositoryPolicy.CheckUpdate(repository, actor), Is.EqualTo(RepositoryAccess.Forbidden));
                Assert.That(repositoryPolicy.CheckDelete(repository, actor), Is.EqualTo(RepositoryAccess.Forbidden));
                Assert.That(packagePolicy.CheckPublish(repository, actor), Is.EqualTo(RepositoryAccess.Forbidden));
                Assert.That(packagePolicy.CheckDelete(repository, actor), Is.EqualTo(RepositoryAccess.Forbidden));
            }

            Assert.That(repositoryPolicy.CheckCreate(actor), Is.EqualTo(RepositoryAccess.Forbidden));

            Assert.That(
                repositories.Select(repositoryPolicy.VisibleTo(actor).Compile()),
                Is.EqualTo(repositories.Select(repositoryPolicy.VisibleTo(Actor.Anonymous).Compile())),
                "repositories");
            Assert.That(
                repositories.Select(packagePolicy.VisibleTo(actor).Compile()),
                Is.EqualTo(repositories.Select(packagePolicy.VisibleTo(Actor.Anonymous).Compile())),
                "packages");
        });
    }

    private static PacmanRepository RepositoryOf(User owner, bool isPublic) => new()
    {
        Name = "a-repo",
        SupportedArchitectures = [Architectures.X86_64],
        OwnerId = owner.Id,
        Owner = owner,
        IsPublic = isPublic,
    };

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

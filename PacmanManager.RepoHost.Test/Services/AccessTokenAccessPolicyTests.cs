using Microsoft.Extensions.Logging.Abstractions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Exercises the access token authorization rules directly. The policy has no database, HTTP or
/// logging dependency, so every row of the rules table can be stated as a plain assertion.
/// </summary>
/// <remarks>
/// Each verdict has the same arms: the system is allowed, no user is unauthenticated, and a user is
/// allowed exactly when the scope permits the verdict's action on tokens together with
/// <c>tokens:read</c>.
/// </remarks>
[TestFixture]
public class AccessTokenAccessPolicyTests
{
    private AccessTokenAccessPolicy _subject = null!;
    private User _user = null!;

    [SetUp]
    public void SetUp()
    {
        _subject = new AccessTokenAccessPolicy();
        _user = new User { DisplayName = "user", NormalizedDisplayName = "user", Email = "user@test.com" };
    }

    #region Every verdict

    [TestCase(nameof(AccessTokenAccessPolicy.CheckRead))]
    [TestCase(nameof(AccessTokenAccessPolicy.CheckCreate))]
    [TestCase(nameof(AccessTokenAccessPolicy.CheckDelete))]
    public void System_IsAllowed(string verdict)
    {
        Assert.Multiple(() =>
        {
            Assert.That(Verdict(verdict, Actor.System), Is.EqualTo(RepositoryAccess.Allowed), "system");
            Assert.That(Verdict(verdict, Actor.SystemFor(_user)), Is.EqualTo(RepositoryAccess.Allowed), "system for a user");
        });
    }

    [TestCase(nameof(AccessTokenAccessPolicy.CheckRead))]
    [TestCase(nameof(AccessTokenAccessPolicy.CheckCreate))]
    [TestCase(nameof(AccessTokenAccessPolicy.CheckDelete))]
    public void Anonymous_IsUnauthenticated(string verdict)
    {
        Assert.That(Verdict(verdict, Actor.Anonymous), Is.EqualTo(RepositoryAccess.Unauthenticated));
    }

    [TestCase(nameof(AccessTokenAccessPolicy.CheckRead))]
    [TestCase(nameof(AccessTokenAccessPolicy.CheckCreate))]
    [TestCase(nameof(AccessTokenAccessPolicy.CheckDelete))]
    public void UnrestrictedUser_IsAllowed(string verdict)
    {
        Assert.That(Verdict(verdict, Actor.For(_user, ActorScope.Unrestricted)), Is.EqualTo(RepositoryAccess.Allowed));
    }

    [TestCase(nameof(AccessTokenAccessPolicy.CheckRead))]
    [TestCase(nameof(AccessTokenAccessPolicy.CheckCreate))]
    [TestCase(nameof(AccessTokenAccessPolicy.CheckDelete))]
    public void UserWithNoneOfOurValues_IsForbidden(string verdict)
    {
        Assert.That(Verdict(verdict, Actor.For(_user, ActorScope.Empty)), Is.EqualTo(RepositoryAccess.Forbidden));
    }

    [TestCase(nameof(AccessTokenAccessPolicy.CheckRead))]
    [TestCase(nameof(AccessTokenAccessPolicy.CheckCreate))]
    [TestCase(nameof(AccessTokenAccessPolicy.CheckDelete))]
    public void UserWithEveryOtherEntity_IsForbidden(string verdict)
    {
        var scope = ScopeOf("pacman-manager:repositories:* pacman-manager:packages:* pacman-manager:users:*");

        Assert.That(Verdict(verdict, Actor.For(_user, scope)), Is.EqualTo(RepositoryAccess.Forbidden));
    }

    [TestCase(nameof(AccessTokenAccessPolicy.CheckRead))]
    [TestCase(nameof(AccessTokenAccessPolicy.CheckCreate))]
    [TestCase(nameof(AccessTokenAccessPolicy.CheckDelete))]
    public void UserWithTokensWildcard_IsAllowed(string verdict)
    {
        Assert.That(Verdict(verdict, Actor.For(_user, ScopeOf("pacman-manager:tokens:*"))), Is.EqualTo(RepositoryAccess.Allowed));
    }

    #endregion

    #region CheckRead

    [TestCase("pacman-manager:tokens:read")]
    [TestCase("pacman-manager:*:read")]
    public void CheckRead_WithTokensRead_IsAllowed(string claim)
    {
        Assert.That(_subject.CheckRead(Actor.For(_user, ScopeOf(claim))), Is.EqualTo(RepositoryAccess.Allowed));
    }

    [TestCase("pacman-manager:tokens:create pacman-manager:tokens:delete")]
    [TestCase("pacman-manager:users:read")]
    public void CheckRead_WithoutTokensRead_IsForbidden(string claim)
    {
        Assert.That(_subject.CheckRead(Actor.For(_user, ScopeOf(claim))), Is.EqualTo(RepositoryAccess.Forbidden));
    }

    #endregion

    #region CheckCreate

    [TestCase("pacman-manager:tokens:create pacman-manager:tokens:read")]
    [TestCase("pacman-manager:*:create pacman-manager:*:read")]
    public void CheckCreate_WithTokensCreateAndRead_IsAllowed(string claim)
    {
        Assert.That(_subject.CheckCreate(Actor.For(_user, ScopeOf(claim))), Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void CheckCreate_WithTokensCreateButNotRead_IsForbidden()
    {
        Assert.That(_subject.CheckCreate(Actor.For(_user, ScopeOf("pacman-manager:tokens:create"))), Is.EqualTo(RepositoryAccess.Forbidden));
    }

    [TestCase("pacman-manager:tokens:read")]
    [TestCase("pacman-manager:*:read")]
    [TestCase("pacman-manager:tokens:read pacman-manager:tokens:delete")]
    public void CheckCreate_WithoutTokensCreate_IsForbidden(string claim)
    {
        // pacman-manager:*:read is the scope a Basic credential carries: it may never mint a token.
        Assert.That(_subject.CheckCreate(Actor.For(_user, ScopeOf(claim))), Is.EqualTo(RepositoryAccess.Forbidden));
    }

    #endregion

    #region CheckDelete

    [TestCase("pacman-manager:tokens:delete pacman-manager:tokens:read")]
    [TestCase("pacman-manager:*:delete pacman-manager:*:read")]
    public void CheckDelete_WithTokensDeleteAndRead_IsAllowed(string claim)
    {
        Assert.That(_subject.CheckDelete(Actor.For(_user, ScopeOf(claim))), Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void CheckDelete_WithTokensDeleteButNotRead_IsForbidden()
    {
        Assert.That(_subject.CheckDelete(Actor.For(_user, ScopeOf("pacman-manager:tokens:delete"))), Is.EqualTo(RepositoryAccess.Forbidden));
    }

    [TestCase("pacman-manager:tokens:read")]
    [TestCase("pacman-manager:*:read")]
    [TestCase("pacman-manager:tokens:read pacman-manager:tokens:create")]
    public void CheckDelete_WithoutTokensDelete_IsForbidden(string claim)
    {
        Assert.That(_subject.CheckDelete(Actor.For(_user, ScopeOf(claim))), Is.EqualTo(RepositoryAccess.Forbidden));
    }

    #endregion

    /// <summary>
    /// Asks the verdict named <paramref name="name"/> about <paramref name="actor"/>.
    /// </summary>
    private RepositoryAccess Verdict(string name, Actor actor) => name switch
    {
        nameof(AccessTokenAccessPolicy.CheckRead) => _subject.CheckRead(actor),
        nameof(AccessTokenAccessPolicy.CheckCreate) => _subject.CheckCreate(actor),
        nameof(AccessTokenAccessPolicy.CheckDelete) => _subject.CheckDelete(actor),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Not a verdict of AccessTokenAccessPolicy."),
    };

    private static ActorScope ScopeOf(string claim) => ActorScope.Parse(claim, NullLogger.Instance);
}

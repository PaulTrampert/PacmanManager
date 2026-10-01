using Microsoft.Extensions.Logging.Abstractions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Exercises the current user's authorization rules directly. The policy has no database, HTTP or
/// logging dependency, so every row of the rules table can be stated as a plain assertion.
/// </summary>
/// <remarks>
/// Each verdict has the same arms: the system is allowed, no user is unauthenticated, and a user is
/// allowed exactly when the scope permits the verdict's action on users together with
/// <c>users:read</c>.
/// </remarks>
[TestFixture]
public class UserAccessPolicyTests
{
    private UserAccessPolicy _subject = null!;
    private User _user = null!;

    [SetUp]
    public void SetUp()
    {
        _subject = new UserAccessPolicy();
        _user = new User { DisplayName = "user", NormalizedDisplayName = "user", Email = "user@test.com" };
    }

    #region Every verdict

    [TestCase(nameof(UserAccessPolicy.CheckReadCurrent))]
    [TestCase(nameof(UserAccessPolicy.CheckUpdateCurrent))]
    public void System_IsAllowed(string verdict)
    {
        Assert.Multiple(() =>
        {
            Assert.That(Verdict(verdict, Actor.System), Is.EqualTo(RepositoryAccess.Allowed), "system");
            Assert.That(Verdict(verdict, Actor.SystemFor(_user)), Is.EqualTo(RepositoryAccess.Allowed), "system for a user");
        });
    }

    [TestCase(nameof(UserAccessPolicy.CheckReadCurrent))]
    [TestCase(nameof(UserAccessPolicy.CheckUpdateCurrent))]
    public void Anonymous_IsUnauthenticated(string verdict)
    {
        Assert.That(Verdict(verdict, Actor.Anonymous), Is.EqualTo(RepositoryAccess.Unauthenticated));
    }

    [TestCase(nameof(UserAccessPolicy.CheckReadCurrent))]
    [TestCase(nameof(UserAccessPolicy.CheckUpdateCurrent))]
    public void UnrestrictedUser_IsAllowed(string verdict)
    {
        Assert.That(Verdict(verdict, Actor.For(_user, ActorScope.Unrestricted)), Is.EqualTo(RepositoryAccess.Allowed));
    }

    [TestCase(nameof(UserAccessPolicy.CheckReadCurrent))]
    [TestCase(nameof(UserAccessPolicy.CheckUpdateCurrent))]
    public void UserWithNoneOfOurValues_IsForbidden(string verdict)
    {
        Assert.That(Verdict(verdict, Actor.For(_user, ActorScope.Empty)), Is.EqualTo(RepositoryAccess.Forbidden));
    }

    [TestCase(nameof(UserAccessPolicy.CheckReadCurrent))]
    [TestCase(nameof(UserAccessPolicy.CheckUpdateCurrent))]
    public void UserWithEveryOtherEntity_IsForbidden(string verdict)
    {
        var scope = ScopeOf("pacman-manager:repositories:* pacman-manager:packages:* pacman-manager:tokens:*");

        Assert.That(Verdict(verdict, Actor.For(_user, scope)), Is.EqualTo(RepositoryAccess.Forbidden));
    }

    [TestCase(nameof(UserAccessPolicy.CheckReadCurrent))]
    [TestCase(nameof(UserAccessPolicy.CheckUpdateCurrent))]
    public void UserWithUsersWildcard_IsAllowed(string verdict)
    {
        Assert.That(Verdict(verdict, Actor.For(_user, ScopeOf("pacman-manager:users:*"))), Is.EqualTo(RepositoryAccess.Allowed));
    }

    #endregion

    #region CheckReadCurrent

    [TestCase("pacman-manager:users:read")]
    [TestCase("pacman-manager:*:read")]
    public void CheckReadCurrent_WithUsersRead_IsAllowed(string claim)
    {
        Assert.That(_subject.CheckReadCurrent(Actor.For(_user, ScopeOf(claim))), Is.EqualTo(RepositoryAccess.Allowed));
    }

    [TestCase("pacman-manager:users:update")]
    [TestCase("pacman-manager:*:update")]
    [TestCase("pacman-manager:tokens:read")]
    public void CheckReadCurrent_WithoutUsersRead_IsForbidden(string claim)
    {
        Assert.That(_subject.CheckReadCurrent(Actor.For(_user, ScopeOf(claim))), Is.EqualTo(RepositoryAccess.Forbidden));
    }

    #endregion

    #region CheckUpdateCurrent

    [TestCase("pacman-manager:users:update pacman-manager:users:read")]
    [TestCase("pacman-manager:*:update pacman-manager:*:read")]
    public void CheckUpdateCurrent_WithUsersUpdateAndRead_IsAllowed(string claim)
    {
        Assert.That(_subject.CheckUpdateCurrent(Actor.For(_user, ScopeOf(claim))), Is.EqualTo(RepositoryAccess.Allowed));
    }

    [TestCase("pacman-manager:users:update")]
    [TestCase("pacman-manager:*:update")]
    public void CheckUpdateCurrent_WithUsersUpdateButNotRead_IsForbidden(string claim)
    {
        Assert.That(_subject.CheckUpdateCurrent(Actor.For(_user, ScopeOf(claim))), Is.EqualTo(RepositoryAccess.Forbidden));
    }

    [TestCase("pacman-manager:users:read")]
    [TestCase("pacman-manager:*:read")]
    [TestCase("pacman-manager:users:read pacman-manager:repositories:update")]
    public void CheckUpdateCurrent_WithoutUsersUpdate_IsForbidden(string claim)
    {
        // pacman-manager:*:read is the scope a Basic credential carries: it may never change the user.
        Assert.That(_subject.CheckUpdateCurrent(Actor.For(_user, ScopeOf(claim))), Is.EqualTo(RepositoryAccess.Forbidden));
    }

    #endregion

    /// <summary>
    /// Asks the verdict named <paramref name="name"/> about <paramref name="actor"/>.
    /// </summary>
    private RepositoryAccess Verdict(string name, Actor actor) => name switch
    {
        nameof(UserAccessPolicy.CheckReadCurrent) => _subject.CheckReadCurrent(actor),
        nameof(UserAccessPolicy.CheckUpdateCurrent) => _subject.CheckUpdateCurrent(actor),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Not a verdict of UserAccessPolicy."),
    };

    private static ActorScope ScopeOf(string claim) => ActorScope.Parse(claim, NullLogger.Instance);
}

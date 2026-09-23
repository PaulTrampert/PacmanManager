using Microsoft.Extensions.Logging.Abstractions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Authentication;

/// <summary>
/// Pins the scope each kind of actor carries. An actor with no credential behind it is unrestricted;
/// one with a credential carries exactly what the credential says.
/// </summary>
[TestFixture]
public class ActorTests
{
    private readonly User _user = new() { DisplayName = "alex", NormalizedDisplayName = "alex", Email = "alex@test.com" };

    [Test]
    public void Anonymous_CarriesTheEmptyScope()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Actor.Anonymous.Scope, Is.EqualTo(ActorScope.Empty));
            Assert.That(Actor.Anonymous.IsReadOnly, Is.True);
        });
    }

    [Test]
    public void System_IsUnrestricted()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Actor.System.Scope, Is.EqualTo(ActorScope.Unrestricted));
            Assert.That(Actor.System.IsReadOnly, Is.False);
        });
    }

    [Test]
    public void SystemFor_IsUnrestricted()
    {
        Assert.That(Actor.SystemFor(_user).Scope, Is.EqualTo(ActorScope.Unrestricted));
    }

    [Test]
    public async Task FixedActorAccessorFor_IsUnrestricted()
    {
        var actor = await FixedActorAccessor.For(_user).GetActorAsync();

        Assert.Multiple(() =>
        {
            Assert.That(actor.User, Is.SameAs(_user));
            Assert.That(actor.IsSystem, Is.False);
            Assert.That(actor.Scope, Is.EqualTo(ActorScope.Unrestricted));
            Assert.That(actor.IsReadOnly, Is.False);
        });
    }

    [Test]
    public async Task FixedActorAccessorSystem_IsUnrestricted()
    {
        var actor = await FixedActorAccessor.System.GetActorAsync();

        Assert.That(actor.Scope, Is.EqualTo(ActorScope.Unrestricted));
    }

    [TestCase("pacman-manager:*:read", ExpectedResult = true)]
    [TestCase("openid profile", ExpectedResult = true)]
    [TestCase("pacman-manager:*:read pacman-manager:tokens:create", ExpectedResult = false)]
    public bool IsReadOnly_FollowsTheScope(string claim) =>
        Actor.For(_user, ActorScope.Parse(claim, NullLogger.Instance)).IsReadOnly;

    [Test]
    public void For_ActorsWithDifferentScopes_AreNotEqual()
    {
        Assert.That(
            Actor.For(_user, ActorScope.Unrestricted),
            Is.Not.EqualTo(Actor.For(_user, ActorScope.Empty)));
    }
}

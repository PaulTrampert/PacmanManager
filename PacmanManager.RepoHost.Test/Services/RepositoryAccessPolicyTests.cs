using Microsoft.Extensions.Logging.Abstractions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Exercises the repository authorization rules directly. The policy has no database, HTTP or
/// logging dependency, so every case in the rules table can be stated as a plain assertion.
/// </summary>
[TestFixture]
public class RepositoryAccessPolicyTests
{
    private RepositoryAccessPolicy _subject = null!;
    private User _owner = null!;
    private User _stranger = null!;

    [SetUp]
    public void SetUp()
    {
        _subject = new RepositoryAccessPolicy();
        _owner = new User { DisplayName = "owner", NormalizedDisplayName = "owner", Email = "owner@test.com" };
        _stranger = new User { DisplayName = "stranger", NormalizedDisplayName = "stranger", Email = "stranger@test.com" };
    }

    #region Visibility

    [Test]
    public void VisibleTo_Anonymous_MatchesPublicRepositoriesOnly()
    {
        var visible = _subject.VisibleTo(Actor.Anonymous).Compile();

        Assert.Multiple(() =>
        {
            Assert.That(visible(RepositoryOf(_owner, isPublic: true)), Is.True);
            Assert.That(visible(RepositoryOf(_owner, isPublic: false)), Is.False);
        });
    }

    [Test]
    public void VisibleTo_User_MatchesPublicRepositoriesAndTheirOwn()
    {
        var visible = _subject.VisibleTo(Actor.For(_owner, ActorScope.Unrestricted)).Compile();

        Assert.Multiple(() =>
        {
            Assert.That(visible(RepositoryOf(_owner, isPublic: false)), Is.True, "own private repository");
            Assert.That(visible(RepositoryOf(_owner, isPublic: true)), Is.True, "own public repository");
            Assert.That(visible(RepositoryOf(_stranger, isPublic: true)), Is.True, "someone else's public repository");
            Assert.That(visible(RepositoryOf(_stranger, isPublic: false)), Is.False, "someone else's private repository");
        });
    }

    [Test]
    public void VisibleTo_System_MatchesEverything()
    {
        var visible = _subject.VisibleTo(Actor.System).Compile();

        Assert.Multiple(() =>
        {
            Assert.That(visible(RepositoryOf(_stranger, isPublic: false)), Is.True);
            Assert.That(visible(RepositoryOf(_stranger, isPublic: true)), Is.True);
        });
    }

    #endregion

    #region Updates

    [Test]
    public void CheckUpdate_Owner_IsAllowed()
    {
        var result = _subject.CheckUpdate(RepositoryOf(_owner, isPublic: false), Actor.For(_owner, ActorScope.Unrestricted));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void CheckUpdate_NonOwnerOfPublicRepository_IsForbidden()
    {
        var result = _subject.CheckUpdate(RepositoryOf(_owner, isPublic: true), Actor.For(_stranger, ActorScope.Unrestricted));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Forbidden));
    }

    [Test]
    public void CheckUpdate_NonOwnerOfPrivateRepository_IsNotFound()
    {
        // Reporting this as forbidden would confirm that the repository exists.
        var result = _subject.CheckUpdate(RepositoryOf(_owner, isPublic: false), Actor.For(_stranger, ActorScope.Unrestricted));

        Assert.That(result, Is.EqualTo(RepositoryAccess.NotFound));
    }

    [Test]
    public void CheckUpdate_Anonymous_IsUnauthenticated()
    {
        var result = _subject.CheckUpdate(RepositoryOf(_owner, isPublic: true), Actor.Anonymous);

        Assert.That(result, Is.EqualTo(RepositoryAccess.Unauthenticated));
    }

    [Test]
    public void CheckUpdate_System_IsAllowed()
    {
        var result = _subject.CheckUpdate(RepositoryOf(_owner, isPublic: false), Actor.System);

        Assert.That(result, Is.EqualTo(RepositoryAccess.Allowed));
    }

    #endregion

    #region Deletes

    // CheckDelete has every row CheckUpdate has. The two are separate verdicts because a caller that
    // may rename a repository need not be able to delete it, not because the rules differ today.

    [Test]
    public void CheckDelete_Owner_IsAllowed()
    {
        var result = _subject.CheckDelete(RepositoryOf(_owner, isPublic: false), Actor.For(_owner, ActorScope.Unrestricted));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void CheckDelete_NonOwnerOfPublicRepository_IsForbidden()
    {
        var result = _subject.CheckDelete(RepositoryOf(_owner, isPublic: true), Actor.For(_stranger, ActorScope.Unrestricted));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Forbidden));
    }

    [Test]
    public void CheckDelete_NonOwnerOfPrivateRepository_IsNotFound()
    {
        // Reporting this as forbidden would confirm that the repository exists.
        var result = _subject.CheckDelete(RepositoryOf(_owner, isPublic: false), Actor.For(_stranger, ActorScope.Unrestricted));

        Assert.That(result, Is.EqualTo(RepositoryAccess.NotFound));
    }

    [Test]
    public void CheckDelete_Anonymous_IsUnauthenticated()
    {
        var result = _subject.CheckDelete(RepositoryOf(_owner, isPublic: true), Actor.Anonymous);

        Assert.That(result, Is.EqualTo(RepositoryAccess.Unauthenticated));
    }

    [Test]
    public void CheckDelete_System_IsAllowed()
    {
        var result = _subject.CheckDelete(RepositoryOf(_owner, isPublic: false), Actor.System);

        Assert.That(result, Is.EqualTo(RepositoryAccess.Allowed));
    }

    #endregion

    #region Creation

    [Test]
    public void CheckCreate_User_IsAllowed()
    {
        Assert.That(_subject.CheckCreate(Actor.For(_owner, ActorScope.Unrestricted)), Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void CheckCreate_Anonymous_IsUnauthenticated()
    {
        Assert.That(_subject.CheckCreate(Actor.Anonymous), Is.EqualTo(RepositoryAccess.Unauthenticated));
    }

    [Test]
    public void CheckCreate_SystemWithoutUser_IsUnauthenticated()
    {
        // A system actor bypasses authorization, but a new repository still needs an owner.
        Assert.That(_subject.CheckCreate(Actor.System), Is.EqualTo(RepositoryAccess.Unauthenticated));
    }

    [Test]
    public void CheckCreate_SystemActingForUser_IsAllowed()
    {
        Assert.That(_subject.CheckCreate(Actor.SystemFor(_owner)), Is.EqualTo(RepositoryAccess.Allowed));
    }

    #endregion

    #region Scope

    // The scope is asked after the no-user rule and before ownership. It only narrows: what it
    // permits still has to pass the ownership rules underneath.

    [TestCase("pacman-manager:repositories:delete")]
    [TestCase("pacman-manager:packages:read")]
    [TestCase("pacman-manager:*:create pacman-manager:*:update pacman-manager:*:delete")]
    public void VisibleTo_ScopeWithoutRepositoriesRead_IsThePublicPredicate(string scope)
    {
        AssertReadsAsAnonymous(Actor.For(_owner, ScopeOf(scope)));
    }

    [Test]
    public void VisibleTo_EmptyScope_IsThePublicPredicate()
    {
        AssertReadsAsAnonymous(Actor.For(_owner, ActorScope.Empty));
    }

    [TestCase("pacman-manager:*:read")]
    [TestCase("pacman-manager:repositories:read")]
    [TestCase("pacman-manager:repositories:*")]
    public void VisibleTo_ScopeWithRepositoriesRead_SeesWhatItsUserSees(string scope)
    {
        var visible = _subject.VisibleTo(Actor.For(_owner, ScopeOf(scope))).Compile();

        Assert.Multiple(() =>
        {
            Assert.That(visible(RepositoryOf(_owner, isPublic: false)), Is.True, "own private repository");
            Assert.That(visible(RepositoryOf(_stranger, isPublic: false)), Is.False, "someone else's private repository");
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ReadOnlyOwner_IsForbiddenEveryChange_NotNotFound(bool isPublic)
    {
        // Their own repository is in their visible set, so refusing the change leaks nothing, and
        // re-authenticating with the same credential would not help.
        var actor = Actor.For(_owner, ScopeOf("pacman-manager:*:read"));
        var repository = RepositoryOf(_owner, isPublic);

        Assert.Multiple(() =>
        {
            Assert.That(_subject.CheckUpdate(repository, actor), Is.EqualTo(RepositoryAccess.Forbidden), "update");
            Assert.That(_subject.CheckDelete(repository, actor), Is.EqualTo(RepositoryAccess.Forbidden), "delete");
            Assert.That(_subject.CheckCreate(actor), Is.EqualTo(RepositoryAccess.Forbidden), "create");
            Assert.That(_subject.VisibleTo(actor).Compile()(repository), Is.True, "still reads it");
        });
    }

    [Test]
    public void ChangeScopeWithoutRepositoriesRead_IsForbiddenOnTheOwnersRepository()
    {
        var repository = RepositoryOf(_owner, isPublic: false);

        Assert.Multiple(() =>
        {
            Assert.That(
                _subject.CheckUpdate(repository, Actor.For(_owner, ScopeOf("pacman-manager:repositories:update"))),
                Is.EqualTo(RepositoryAccess.Forbidden), "update");
            Assert.That(
                _subject.CheckDelete(repository, Actor.For(_owner, ScopeOf("pacman-manager:repositories:delete"))),
                Is.EqualTo(RepositoryAccess.Forbidden), "delete");
            Assert.That(
                _subject.CheckCreate(Actor.For(_owner, ScopeOf("pacman-manager:repositories:create"))),
                Is.EqualTo(RepositoryAccess.Forbidden), "create");
        });
    }

    [Test]
    public void ChangeScopeWithRepositoriesRead_IsAllowedForTheOwner()
    {
        var repository = RepositoryOf(_owner, isPublic: false);

        Assert.Multiple(() =>
        {
            Assert.That(
                _subject.CheckUpdate(repository, Actor.For(_owner, ScopeOf("pacman-manager:repositories:update pacman-manager:repositories:read"))),
                Is.EqualTo(RepositoryAccess.Allowed), "update");
            Assert.That(
                _subject.CheckDelete(repository, Actor.For(_owner, ScopeOf("pacman-manager:repositories:delete pacman-manager:*:read"))),
                Is.EqualTo(RepositoryAccess.Allowed), "delete");
            Assert.That(
                _subject.CheckCreate(Actor.For(_owner, ScopeOf("pacman-manager:repositories:create pacman-manager:repositories:read"))),
                Is.EqualTo(RepositoryAccess.Allowed), "create");
            Assert.That(
                _subject.CheckUpdate(repository, Actor.For(_owner, ScopeOf("pacman-manager:repositories:*"))),
                Is.EqualTo(RepositoryAccess.Allowed), "every action on repositories");
        });
    }

    [Test]
    public void UpdateAndDelete_AreSeparateScopes()
    {
        var updater = Actor.For(_owner, ScopeOf("pacman-manager:repositories:read pacman-manager:repositories:update"));
        var deleter = Actor.For(_owner, ScopeOf("pacman-manager:repositories:read pacman-manager:repositories:delete"));
        var repository = RepositoryOf(_owner, isPublic: false);

        Assert.Multiple(() =>
        {
            Assert.That(_subject.CheckDelete(repository, updater), Is.EqualTo(RepositoryAccess.Forbidden));
            Assert.That(_subject.CheckUpdate(repository, deleter), Is.EqualTo(RepositoryAccess.Forbidden));
            Assert.That(_subject.CheckCreate(updater), Is.EqualTo(RepositoryAccess.Forbidden));
        });
    }

    [Test]
    public void ScopeThatPermitsTheChange_StillLosesToOwnership()
    {
        // A scope is a ceiling, never a grant: somebody else's repository is still refused.
        var actor = Actor.For(_stranger, ScopeOf("pacman-manager:repositories:*"));

        Assert.Multiple(() =>
        {
            Assert.That(_subject.CheckUpdate(RepositoryOf(_owner, isPublic: true), actor), Is.EqualTo(RepositoryAccess.Forbidden));
            Assert.That(_subject.CheckDelete(RepositoryOf(_owner, isPublic: true), actor), Is.EqualTo(RepositoryAccess.Forbidden));
            Assert.That(_subject.CheckUpdate(RepositoryOf(_owner, isPublic: false), actor), Is.EqualTo(RepositoryAccess.NotFound));
            Assert.That(_subject.CheckDelete(RepositoryOf(_owner, isPublic: false), actor), Is.EqualTo(RepositoryAccess.NotFound));
        });
    }

    [Test]
    public void EmptyScope_IsForbiddenEveryChange()
    {
        // Its user is known, so the refusal is a 403 rather than a challenge.
        var actor = Actor.For(_owner, ActorScope.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(_subject.CheckUpdate(RepositoryOf(_owner, isPublic: true), actor), Is.EqualTo(RepositoryAccess.Forbidden));
            Assert.That(_subject.CheckDelete(RepositoryOf(_owner, isPublic: true), actor), Is.EqualTo(RepositoryAccess.Forbidden));
            Assert.That(_subject.CheckCreate(actor), Is.EqualTo(RepositoryAccess.Forbidden));
        });
    }

    private void AssertReadsAsAnonymous(Actor actor)
    {
        var repositories = new[]
        {
            RepositoryOf(_owner, isPublic: false),
            RepositoryOf(_owner, isPublic: true),
            RepositoryOf(_stranger, isPublic: false),
            RepositoryOf(_stranger, isPublic: true),
        };
        var visible = _subject.VisibleTo(actor).Compile();
        var anonymous = _subject.VisibleTo(Actor.Anonymous).Compile();

        Assert.That(repositories.Select(visible), Is.EqualTo(repositories.Select(anonymous)));
        Assert.That(visible(RepositoryOf(_owner, isPublic: false)), Is.False, "the owner's own private repository");
    }

    private static ActorScope ScopeOf(string claim) => ActorScope.Parse(claim, NullLogger.Instance);

    #endregion

    private static PacmanRepository RepositoryOf(User owner, bool isPublic) => new()
    {
        Name = "a-repo",
        Architecture = "x86_64",
        OwnerId = owner.Id,
        Owner = owner,
        IsPublic = isPublic,
    };
}

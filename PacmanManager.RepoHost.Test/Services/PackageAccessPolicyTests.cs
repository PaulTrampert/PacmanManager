using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Exercises the package authorization rules directly. The policy has no database, HTTP or
/// logging dependency, so every row of the rules table can be stated as a plain assertion.
/// </summary>
[TestFixture]
public class PackageAccessPolicyTests
{
    private PackageAccessPolicy _subject = null!;
    private RepositoryAccessPolicy _repositoryPolicy = null!;
    private User _owner = null!;
    private User _stranger = null!;

    [SetUp]
    public void SetUp()
    {
        _repositoryPolicy = new RepositoryAccessPolicy();
        _subject = new PackageAccessPolicy(_repositoryPolicy);
        _owner = new User { DisplayName = "owner", NormalizedDisplayName = "owner", Email = "owner@test.com" };
        _stranger = new User { DisplayName = "stranger", NormalizedDisplayName = "stranger", Email = "stranger@test.com" };
    }

    #region Reads

    // Packages have no visibility of their own: a package is visible exactly when its repository
    // is. PackageAccessPolicy.VisibleTo is therefore a predicate over the repository, and composes
    // RepositoryAccessPolicy.VisibleTo rather than restating it.

    [Test]
    public void Read_PublicRepository_IsVisibleToAnonymous()
    {
        Assert.That(PackageIsVisible(RepositoryOf(_owner, isPublic: true), Actor.Anonymous), Is.True);
    }

    [Test]
    public void Read_PrivateRepository_IsVisibleToTheOwner()
    {
        Assert.That(PackageIsVisible(RepositoryOf(_owner, isPublic: false), Actor.For(_owner, ActorScope.Unrestricted)), Is.True);
    }

    [Test]
    public void Read_PrivateRepository_IsNotVisibleToAnyoneElse()
    {
        // Absent from the visible set is what makes the read a 404 rather than a 403.
        Assert.Multiple(() =>
        {
            Assert.That(PackageIsVisible(RepositoryOf(_owner, isPublic: false), Actor.For(_stranger, ActorScope.Unrestricted)), Is.False);
            Assert.That(PackageIsVisible(RepositoryOf(_owner, isPublic: false), Actor.Anonymous), Is.False);
        });
    }

    #endregion

    #region Publish and replace

    // CheckPublish answers for a first publish and a replacement alike: the permission is held over
    // the repository, so whether the package is already there does not change the verdict.

    [Test]
    public void CheckPublish_OwnerOfPrivateRepository_IsAllowed()
    {
        var result = _subject.CheckPublish(RepositoryOf(_owner, isPublic: false), Actor.For(_owner, ActorScope.Unrestricted));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void CheckPublish_OwnerOfPublicRepository_IsAllowed()
    {
        var result = _subject.CheckPublish(RepositoryOf(_owner, isPublic: true), Actor.For(_owner, ActorScope.Unrestricted));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void CheckPublish_NonOwnerOfPublicRepository_IsForbidden()
    {
        // The repository is public, so admitting it exists and refusing the publish leaks nothing.
        var result = _subject.CheckPublish(RepositoryOf(_owner, isPublic: true), Actor.For(_stranger, ActorScope.Unrestricted));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Forbidden));
    }

    [Test]
    public void CheckPublish_NonOwnerOfPrivateRepository_IsNotFound()
    {
        // Reporting this as forbidden would confirm that the repository exists. In practice such a
        // repository never reaches the check, being already absent from the visible set.
        var result = _subject.CheckPublish(RepositoryOf(_owner, isPublic: false), Actor.For(_stranger, ActorScope.Unrestricted));

        Assert.That(result, Is.EqualTo(RepositoryAccess.NotFound));
    }

    [Test]
    public void CheckPublish_Anonymous_IsUnauthenticated()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                _subject.CheckPublish(RepositoryOf(_owner, isPublic: true), Actor.Anonymous),
                Is.EqualTo(RepositoryAccess.Unauthenticated),
                "public repository");
            Assert.That(
                _subject.CheckPublish(RepositoryOf(_owner, isPublic: false), Actor.Anonymous),
                Is.EqualTo(RepositoryAccess.Unauthenticated),
                "private repository");
        });
    }

    [Test]
    public void CheckPublish_System_IsAllowed()
    {
        var result = _subject.CheckPublish(RepositoryOf(_stranger, isPublic: false), Actor.System);

        Assert.That(result, Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void CheckPublish_PublisherOfAPackageWhoDoesNotOwnTheRepository_IsForbidden()
    {
        // Having published a package grants nothing: the permission is held over the repository,
        // so CheckPublish takes no package argument to consult in the first place.
        var result = _subject.CheckPublish(RepositoryOf(_owner, isPublic: true), Actor.For(_stranger, ActorScope.Unrestricted));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Forbidden));
    }

    #endregion

    #region Delete

    // CheckDelete has every row CheckPublish has. The two are separate verdicts because a caller
    // that may publish need not be able to delete, not because the rules differ today.

    [Test]
    public void CheckDelete_OwnerOfPrivateRepository_IsAllowed()
    {
        var result = _subject.CheckDelete(RepositoryOf(_owner, isPublic: false), Actor.For(_owner, ActorScope.Unrestricted));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void CheckDelete_OwnerOfPublicRepository_IsAllowed()
    {
        var result = _subject.CheckDelete(RepositoryOf(_owner, isPublic: true), Actor.For(_owner, ActorScope.Unrestricted));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void CheckDelete_NonOwnerOfPublicRepository_IsForbidden()
    {
        // The repository is public, so admitting it exists and refusing the delete leaks nothing.
        var result = _subject.CheckDelete(RepositoryOf(_owner, isPublic: true), Actor.For(_stranger, ActorScope.Unrestricted));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Forbidden));
    }

    [Test]
    public void CheckDelete_NonOwnerOfPrivateRepository_IsNotFound()
    {
        // Reporting this as forbidden would confirm that the repository exists. In practice such a
        // repository never reaches the check, being already absent from the visible set.
        var result = _subject.CheckDelete(RepositoryOf(_owner, isPublic: false), Actor.For(_stranger, ActorScope.Unrestricted));

        Assert.That(result, Is.EqualTo(RepositoryAccess.NotFound));
    }

    [Test]
    public void CheckDelete_Anonymous_IsUnauthenticated()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                _subject.CheckDelete(RepositoryOf(_owner, isPublic: true), Actor.Anonymous),
                Is.EqualTo(RepositoryAccess.Unauthenticated),
                "public repository");
            Assert.That(
                _subject.CheckDelete(RepositoryOf(_owner, isPublic: false), Actor.Anonymous),
                Is.EqualTo(RepositoryAccess.Unauthenticated),
                "private repository");
        });
    }

    [Test]
    public void CheckDelete_System_IsAllowed()
    {
        var result = _subject.CheckDelete(RepositoryOf(_stranger, isPublic: false), Actor.System);

        Assert.That(result, Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void CheckDelete_PublisherOfAPackageWhoDoesNotOwnTheRepository_IsForbidden()
    {
        // Having published a package grants nothing: the permission is held over the repository,
        // so CheckDelete takes no package argument to consult in the first place.
        var result = _subject.CheckDelete(RepositoryOf(_owner, isPublic: true), Actor.For(_stranger, ActorScope.Unrestricted));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Forbidden));
    }

    #endregion

    #region Scope

    // The scope is asked after the no-user rule and before the repository grant. It only narrows:
    // what it permits still has to pass the grant underneath.

    [Test]
    public void VisibleTo_ScopeWithRepositoriesReadButNotPackagesRead_IsThePublicPredicate()
    {
        var actor = Actor.For(_owner, ScopeOf("pacman-manager:repositories:read"));

        AssertReadsAsAnonymous(actor);
    }

    [Test]
    public void VisibleTo_ScopeWithPackagesReadButNotRepositoriesRead_IsThePublicPredicate()
    {
        // A package is read through its repository, so reading one needs both.
        var actor = Actor.For(_owner, ScopeOf("pacman-manager:packages:read"));

        AssertReadsAsAnonymous(actor);
    }

    [Test]
    public void VisibleTo_EmptyScope_IsThePublicPredicate()
    {
        AssertReadsAsAnonymous(Actor.For(_owner, ActorScope.Empty));
    }

    [TestCase("pacman-manager:*:read")]
    [TestCase("pacman-manager:packages:read pacman-manager:repositories:read")]
    public void VisibleTo_ScopeThatReadsBoth_SeesTheOwnersPrivateRepository(string scope)
    {
        var actor = Actor.For(_owner, ScopeOf(scope));

        Assert.That(PackageIsVisible(RepositoryOf(_owner, isPublic: false), actor), Is.True);
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
            Assert.That(_subject.CheckPublish(repository, actor), Is.EqualTo(RepositoryAccess.Forbidden), "publish");
            Assert.That(_subject.CheckDelete(repository, actor), Is.EqualTo(RepositoryAccess.Forbidden), "delete");
            Assert.That(PackageIsVisible(repository, actor), Is.True, "still reads it");
        });
    }

    [TestCase("pacman-manager:packages:create")]
    [TestCase("pacman-manager:packages:create pacman-manager:packages:read")]
    [TestCase("pacman-manager:packages:create pacman-manager:repositories:read")]
    public void CheckPublish_ChangeScopeWithoutBothReadScopes_IsForbiddenOnTheOwnersRepository(string scope)
    {
        var result = _subject.CheckPublish(RepositoryOf(_owner, isPublic: false), Actor.For(_owner, ScopeOf(scope)));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Forbidden));
    }

    [TestCase("pacman-manager:packages:delete")]
    [TestCase("pacman-manager:packages:delete pacman-manager:packages:read")]
    [TestCase("pacman-manager:packages:delete pacman-manager:repositories:read")]
    public void CheckDelete_ChangeScopeWithoutBothReadScopes_IsForbiddenOnTheOwnersRepository(string scope)
    {
        var result = _subject.CheckDelete(RepositoryOf(_owner, isPublic: false), Actor.For(_owner, ScopeOf(scope)));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Forbidden));
    }

    [TestCase("pacman-manager:packages:create pacman-manager:packages:read pacman-manager:repositories:read")]
    [TestCase("pacman-manager:packages:* pacman-manager:repositories:read")]
    public void CheckPublish_ScopeWithCreateAndBothReads_IsAllowedForTheOwner(string scope)
    {
        var result = _subject.CheckPublish(RepositoryOf(_owner, isPublic: false), Actor.For(_owner, ScopeOf(scope)));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Allowed));
    }

    [TestCase("pacman-manager:packages:delete pacman-manager:packages:read pacman-manager:repositories:read")]
    [TestCase("pacman-manager:packages:* pacman-manager:repositories:read")]
    public void CheckDelete_ScopeWithDeleteAndBothReads_IsAllowedForTheOwner(string scope)
    {
        var result = _subject.CheckDelete(RepositoryOf(_owner, isPublic: false), Actor.For(_owner, ScopeOf(scope)));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void PublishAndDelete_AreSeparateScopes()
    {
        var reads = "pacman-manager:packages:read pacman-manager:repositories:read";
        var publisher = Actor.For(_owner, ScopeOf($"{reads} pacman-manager:packages:create"));
        var deleter = Actor.For(_owner, ScopeOf($"{reads} pacman-manager:packages:delete"));
        var repository = RepositoryOf(_owner, isPublic: false);

        Assert.Multiple(() =>
        {
            Assert.That(_subject.CheckDelete(repository, publisher), Is.EqualTo(RepositoryAccess.Forbidden));
            Assert.That(_subject.CheckPublish(repository, deleter), Is.EqualTo(RepositoryAccess.Forbidden));
        });
    }

    [Test]
    public void ScopeThatPermitsTheChange_StillLosesToTheRepositoryGrant()
    {
        // A scope is a ceiling, never a grant: somebody else's repository is still refused.
        var actor = Actor.For(_stranger, ScopeOf("pacman-manager:packages:* pacman-manager:repositories:read"));

        Assert.Multiple(() =>
        {
            Assert.That(_subject.CheckPublish(RepositoryOf(_owner, isPublic: true), actor), Is.EqualTo(RepositoryAccess.Forbidden));
            Assert.That(_subject.CheckDelete(RepositoryOf(_owner, isPublic: true), actor), Is.EqualTo(RepositoryAccess.Forbidden));
            Assert.That(_subject.CheckPublish(RepositoryOf(_owner, isPublic: false), actor), Is.EqualTo(RepositoryAccess.NotFound));
            Assert.That(_subject.CheckDelete(RepositoryOf(_owner, isPublic: false), actor), Is.EqualTo(RepositoryAccess.NotFound));
        });
    }

    [Test]
    public void EmptyScope_IsForbiddenEveryChange()
    {
        var actor = Actor.For(_owner, ActorScope.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(_subject.CheckPublish(RepositoryOf(_owner, isPublic: true), actor), Is.EqualTo(RepositoryAccess.Forbidden));
            Assert.That(_subject.CheckDelete(RepositoryOf(_owner, isPublic: true), actor), Is.EqualTo(RepositoryAccess.Forbidden));
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

        Assert.That(
            repositories.Select(r => PackageIsVisible(r, actor)),
            Is.EqualTo(repositories.Select(r => PackageIsVisible(r, Actor.Anonymous))));
        Assert.That(PackageIsVisible(RepositoryOf(_owner, isPublic: false), actor), Is.False,
            "the owner's own private repository");
    }

    #endregion

    private bool PackageIsVisible(PacmanRepository repository, Actor actor)
    {
        Expression<Func<PacmanRepository, bool>> visible = _subject.VisibleTo(actor);
        return visible.Compile()(repository);
    }

    private static ActorScope ScopeOf(string claim) => ActorScope.Parse(claim, NullLogger.Instance);

    private static PacmanRepository RepositoryOf(User owner, bool isPublic) => new()
    {
        Name = "a-repo",
        Architecture = "x86_64",
        OwnerId = owner.Id,
        Owner = owner,
        IsPublic = isPublic,
    };
}

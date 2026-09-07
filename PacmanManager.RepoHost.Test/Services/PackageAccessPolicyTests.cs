using System.Linq.Expressions;
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
        _subject = new PackageAccessPolicy();
        _repositoryPolicy = new RepositoryAccessPolicy();
        _owner = new User { DisplayName = "owner", Email = "owner@test.com" };
        _stranger = new User { DisplayName = "stranger", Email = "stranger@test.com" };
    }

    #region Reads

    // Packages have no visibility of their own: a package is visible exactly when its repository
    // is. These cases therefore assert the composition PackageService performs — a package is
    // readable when RepositoryAccessPolicy.VisibleTo matches the repository it belongs to — rather
    // than a second copy of the rule on PackageAccessPolicy.

    [Test]
    public void Read_PublicRepository_IsVisibleToAnonymous()
    {
        Assert.That(PackageIsVisible(RepositoryOf(_owner, isPublic: true), Actor.Anonymous), Is.True);
    }

    [Test]
    public void Read_PrivateRepository_IsVisibleToTheOwner()
    {
        Assert.That(PackageIsVisible(RepositoryOf(_owner, isPublic: false), Actor.For(_owner)), Is.True);
    }

    [Test]
    public void Read_PrivateRepository_IsNotVisibleToAnyoneElse()
    {
        // Absent from the visible set is what makes the read a 404 rather than a 403.
        Assert.Multiple(() =>
        {
            Assert.That(PackageIsVisible(RepositoryOf(_owner, isPublic: false), Actor.For(_stranger)), Is.False);
            Assert.That(PackageIsVisible(RepositoryOf(_owner, isPublic: false), Actor.Anonymous), Is.False);
        });
    }

    #endregion

    #region Publish, replace and delete

    // CheckPublish answers for publish, replace and delete alike: the permission is held over the
    // repository, so the operation being attempted does not change the verdict.

    [Test]
    public void CheckPublish_OwnerOfPrivateRepository_IsAllowed()
    {
        var result = _subject.CheckPublish(RepositoryOf(_owner, isPublic: false), Actor.For(_owner));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void CheckPublish_OwnerOfPublicRepository_IsAllowed()
    {
        var result = _subject.CheckPublish(RepositoryOf(_owner, isPublic: true), Actor.For(_owner));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void CheckPublish_NonOwnerOfPublicRepository_IsForbidden()
    {
        // The repository is public, so admitting it exists and refusing the publish leaks nothing.
        var result = _subject.CheckPublish(RepositoryOf(_owner, isPublic: true), Actor.For(_stranger));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Forbidden));
    }

    [Test]
    public void CheckPublish_NonOwnerOfPrivateRepository_IsNotFound()
    {
        // Reporting this as forbidden would confirm that the repository exists. In practice such a
        // repository never reaches the check, being already absent from the visible set.
        var result = _subject.CheckPublish(RepositoryOf(_owner, isPublic: false), Actor.For(_stranger));

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
        var result = _subject.CheckPublish(RepositoryOf(_owner, isPublic: true), Actor.For(_stranger));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Forbidden));
    }

    #endregion

    private bool PackageIsVisible(PacmanRepository repository, Actor actor)
    {
        Expression<Func<PacmanRepository, bool>> visible = _repositoryPolicy.VisibleTo(actor);
        return visible.Compile()(repository);
    }

    private static PacmanRepository RepositoryOf(User owner, bool isPublic) => new()
    {
        Name = "a-repo",
        Architecture = "x86_64",
        OwnerId = owner.Id,
        Owner = owner,
        IsPublic = isPublic,
    };
}

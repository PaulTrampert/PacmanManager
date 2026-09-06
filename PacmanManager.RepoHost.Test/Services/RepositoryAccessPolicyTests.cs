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
        _owner = new User { DisplayName = "owner", Email = "owner@test.com" };
        _stranger = new User { DisplayName = "stranger", Email = "stranger@test.com" };
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
        var visible = _subject.VisibleTo(Actor.For(_owner)).Compile();

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

    #region Writes

    [Test]
    public void CheckWrite_Owner_IsAllowed()
    {
        var result = _subject.CheckWrite(RepositoryOf(_owner, isPublic: false), Actor.For(_owner));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void CheckWrite_NonOwnerOfPublicRepository_IsForbidden()
    {
        var result = _subject.CheckWrite(RepositoryOf(_owner, isPublic: true), Actor.For(_stranger));

        Assert.That(result, Is.EqualTo(RepositoryAccess.Forbidden));
    }

    [Test]
    public void CheckWrite_NonOwnerOfPrivateRepository_IsNotFound()
    {
        // Reporting this as forbidden would confirm that the repository exists.
        var result = _subject.CheckWrite(RepositoryOf(_owner, isPublic: false), Actor.For(_stranger));

        Assert.That(result, Is.EqualTo(RepositoryAccess.NotFound));
    }

    [Test]
    public void CheckWrite_Anonymous_IsUnauthenticated()
    {
        var result = _subject.CheckWrite(RepositoryOf(_owner, isPublic: true), Actor.Anonymous);

        Assert.That(result, Is.EqualTo(RepositoryAccess.Unauthenticated));
    }

    [Test]
    public void CheckWrite_System_IsAllowed()
    {
        var result = _subject.CheckWrite(RepositoryOf(_owner, isPublic: false), Actor.System);

        Assert.That(result, Is.EqualTo(RepositoryAccess.Allowed));
    }

    #endregion

    #region Creation

    [Test]
    public void CheckCreate_User_IsAllowed()
    {
        Assert.That(_subject.CheckCreate(Actor.For(_owner)), Is.EqualTo(RepositoryAccess.Allowed));
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

    private static PacmanRepository RepositoryOf(User owner, bool isPublic) => new()
    {
        Name = "a-repo",
        Architecture = "x86_64",
        OwnerId = owner.Id,
        Owner = owner,
        IsPublic = isPublic,
    };
}

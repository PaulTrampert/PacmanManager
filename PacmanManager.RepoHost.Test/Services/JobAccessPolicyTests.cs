using PacmanManager.Authentication;
using PacmanManager.Entities;
using PacmanManager.Packages.Authorization;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Exercises the job visibility rules directly. The policy has no database, HTTP or logging
/// dependency, so every rule can be stated as a plain assertion.
/// </summary>
[TestFixture]
public class JobAccessPolicyTests
{
    private JobAccessPolicy _subject = null!;
    private User _scheduler = null!;
    private User _other = null!;

    [SetUp]
    public void SetUp()
    {
        _subject = new JobAccessPolicy();
        _scheduler = new User { Id = Guid.NewGuid(), DisplayName = "scheduler", NormalizedDisplayName = "scheduler", Email = "scheduler@test.com" };
        _other = new User { Id = Guid.NewGuid(), DisplayName = "other", NormalizedDisplayName = "other", Email = "other@test.com" };
    }

    [Test]
    public void CheckRead_TheUserWhoScheduledTheJob_IsAllowed()
    {
        var verdict = _subject.CheckRead(JobFor(_scheduler.Id), Actor.For(_scheduler, ActorScope.Unrestricted));

        Assert.That(verdict, Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void CheckRead_TheUserWhoScheduledTheJob_IsAllowedWhateverTheScope()
    {
        var verdict = _subject.CheckRead(JobFor(_scheduler.Id), Actor.For(_scheduler, ActorScope.Empty));

        Assert.That(verdict, Is.EqualTo(RepositoryAccess.Allowed));
    }

    [Test]
    public void CheckRead_AnotherUser_IsNotFound()
    {
        var verdict = _subject.CheckRead(JobFor(_scheduler.Id), Actor.For(_other, ActorScope.Unrestricted));

        Assert.That(verdict, Is.EqualTo(RepositoryAccess.NotFound));
    }

    [Test]
    public void CheckRead_AUser_OfAJobTheSystemScheduled_IsNotFound()
    {
        var verdict = _subject.CheckRead(JobFor(null), Actor.For(_scheduler, ActorScope.Unrestricted));

        Assert.That(verdict, Is.EqualTo(RepositoryAccess.NotFound));
    }

    [Test]
    public void CheckRead_AUser_OfNoSuchJob_IsNotFound()
    {
        var verdict = _subject.CheckRead(null, Actor.For(_scheduler, ActorScope.Unrestricted));

        Assert.That(verdict, Is.EqualTo(RepositoryAccess.NotFound));
    }

    [Test]
    public void CheckRead_Anonymous_IsUnauthenticated()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_subject.CheckRead(JobFor(_scheduler.Id), Actor.Anonymous), Is.EqualTo(RepositoryAccess.Unauthenticated), "a user's job");
            Assert.That(_subject.CheckRead(JobFor(null), Actor.Anonymous), Is.EqualTo(RepositoryAccess.Unauthenticated), "the system's job");
            Assert.That(_subject.CheckRead(null, Actor.Anonymous), Is.EqualTo(RepositoryAccess.Unauthenticated), "no such job");
        });
    }

    [Test]
    public void CheckRead_System_IsAllowed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_subject.CheckRead(JobFor(_scheduler.Id), Actor.System), Is.EqualTo(RepositoryAccess.Allowed), "system, a user's job");
            Assert.That(_subject.CheckRead(JobFor(null), Actor.System), Is.EqualTo(RepositoryAccess.Allowed), "system, the system's job");
            Assert.That(_subject.CheckRead(JobFor(_scheduler.Id), Actor.SystemFor(_other)), Is.EqualTo(RepositoryAccess.Allowed), "system for another user");
        });
    }

    [Test]
    public void CheckRead_System_OfNoSuchJob_IsNotFound()
    {
        Assert.That(_subject.CheckRead(null, Actor.System), Is.EqualTo(RepositoryAccess.NotFound));
    }

    private static Job JobFor(Guid? userId) => new()
    {
        UserId = userId,
        MessageType = "Test.Message",
        Message = "{}",
    };
}

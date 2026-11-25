using PacmanManager.Authentication;
using PacmanManager.Entities;
using PacmanManager.Packages.Authorization;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Exercises the job visibility rules directly, for one job and for a listing. The policy has no database, HTTP or logging
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

    [Test]
    public void CheckList_AUser_IsAllowedWhateverTheScope()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_subject.CheckList(Actor.For(_scheduler, ActorScope.Unrestricted)), Is.EqualTo(RepositoryAccess.Allowed), "unrestricted");
            Assert.That(_subject.CheckList(Actor.For(_scheduler, ActorScope.Empty)), Is.EqualTo(RepositoryAccess.Allowed), "empty scope");
        });
    }

    [Test]
    public void CheckList_Anonymous_IsUnauthenticated()
    {
        Assert.That(_subject.CheckList(Actor.Anonymous), Is.EqualTo(RepositoryAccess.Unauthenticated));
    }

    [Test]
    public void CheckList_System_IsAllowed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_subject.CheckList(Actor.System), Is.EqualTo(RepositoryAccess.Allowed), "system");
            Assert.That(_subject.CheckList(Actor.SystemFor(_other)), Is.EqualTo(RepositoryAccess.Allowed), "system for a user");
        });
    }

    [Test]
    public void VisibleTo_AUser_SeesOnlyTheJobsTheyScheduled()
    {
        var visible = Visible(Actor.For(_scheduler, ActorScope.Unrestricted));

        Assert.Multiple(() =>
        {
            Assert.That(visible(JobFor(_scheduler.Id)), Is.True, "their own job");
            Assert.That(visible(JobFor(_other.Id)), Is.False, "another user's job");
            Assert.That(visible(JobFor(null)), Is.False, "a job the system scheduled");
        });
    }

    [Test]
    public void VisibleTo_AUser_SeesTheirOwnJobsWhateverTheScope()
    {
        Assert.That(Visible(Actor.For(_scheduler, ActorScope.Empty))(JobFor(_scheduler.Id)), Is.True);
    }

    [Test]
    public void VisibleTo_Anonymous_SeesNothing()
    {
        var visible = Visible(Actor.Anonymous);

        Assert.Multiple(() =>
        {
            Assert.That(visible(JobFor(_scheduler.Id)), Is.False, "a user's job");
            Assert.That(visible(JobFor(null)), Is.False, "the system's job");
        });
    }

    [Test]
    public void VisibleTo_System_SeesEveryJob()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Visible(Actor.System)(JobFor(_scheduler.Id)), Is.True, "system, a user's job");
            Assert.That(Visible(Actor.System)(JobFor(null)), Is.True, "system, the system's job");
            Assert.That(Visible(Actor.SystemFor(_other))(JobFor(_scheduler.Id)), Is.True, "system for another user");
        });
    }

    [Test]
    public void VisibleTo_AgreesWithCheckRead()
    {
        // A job is in an actor's listing exactly when the actor may read it on its own.
        var actors = new[]
        {
            Actor.For(_scheduler, ActorScope.Unrestricted),
            Actor.For(_other, ActorScope.Empty),
            Actor.System,
            Actor.SystemFor(_other),
        };
        var jobs = new[] { JobFor(_scheduler.Id), JobFor(_other.Id), JobFor(null) };

        Assert.Multiple(() =>
        {
            foreach (var actor in actors)
            {
                var visible = Visible(actor);
                foreach (var job in jobs)
                {
                    Assert.That(
                        visible(job),
                        Is.EqualTo(_subject.CheckRead(job, actor) == RepositoryAccess.Allowed),
                        $"actor {actor.User?.DisplayName ?? "system"}, job of {job.UserId?.ToString() ?? "the system"}");
                }
            }
        });
    }

    private Func<Job, bool> Visible(Actor actor) => _subject.VisibleTo(actor).Compile();

    private static Job JobFor(Guid? userId) => new()
    {
        UserId = userId,
        MessageType = "Test.Message",
        Message = "{}",
    };
}

using Microsoft.EntityFrameworkCore;
using PacmanManager.Authentication;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Tests for <see cref="JobService"/> against the in-memory provider. <see cref="JobAccessPolicyTests"/>
/// covers every rule; these show that the service asks the policy and acts on its verdict, and how a
/// job is shown.
/// </summary>
[TestFixture]
public class JobServiceTests
{
    private string _databaseName = null!;
    private PacmanManagerDbContext _dbContext = null!;
    private TestActorAccessor _actors = null!;
    private JobService _service = null!;

    private User _scheduler = null!;
    private User _other = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseName = Guid.NewGuid().ToString();
        _dbContext = NewContext();

        using (var seed = NewContext())
        {
            _scheduler = seed.Add(new User { DisplayName = "scheduler", NormalizedDisplayName = "scheduler", Email = "scheduler@test.com" }).Entity;
            _other = seed.Add(new User { DisplayName = "other", NormalizedDisplayName = "other", Email = "other@test.com" }).Entity;
            seed.SaveChanges();
        }

        _actors = new TestActorAccessor { Actor = Actor.For(_scheduler, ActorScope.Unrestricted) };
        _service = new JobService(_dbContext, _actors, new JobAccessPolicy());
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    [Test]
    public async Task GetJob_TheSchedulersOwnJob_IsReturned()
    {
        var job = Seed(_scheduler.Id, JobState.Pending);

        var result = await _service.GetJobAsync(job.Id);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Id, Is.EqualTo(job.Id));
            Assert.That(result.State, Is.EqualTo(JobState.Pending));
            Assert.That(result.CreatedAt, Is.EqualTo(job.CreatedAt));
            Assert.That(result.AffectedEntities, Is.Empty);
            Assert.That(result.Error, Is.Null);
        });
    }

    [Test]
    public async Task GetJob_ReturnsItsAffectedEntities_RepositoryFirst()
    {
        var repositoryId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var job = Seed(_scheduler.Id, JobState.Completed,
            (JobEntityType.Package, packageId),
            (JobEntityType.Repository, repositoryId));

        var result = await _service.GetJobAsync(job.Id);

        Assert.That(
            result!.AffectedEntities.Select(e => (e.Type, e.Id)),
            Is.EqualTo(new[] { (JobEntityType.Repository, repositoryId), (JobEntityType.Package, packageId) }));
    }

    [Test]
    public async Task GetJob_Failed_CarriesItsError()
    {
        var job = Seed(_scheduler.Id, JobState.Failed, error: "unsupported architecture");

        var result = await _service.GetJobAsync(job.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result!.State, Is.EqualTo(JobState.Failed));
            Assert.That(result.Error, Is.EqualTo("unsupported architecture"));
        });
    }

    [TestCase(JobState.Pending)]
    [TestCase(JobState.Running)]
    [TestCase(JobState.Completed)]
    public async Task GetJob_NotFailed_CarriesNoError(JobState state)
    {
        // An error left over from a failed attempt that a retry got past is not the job's outcome.
        var job = Seed(_scheduler.Id, state, error: "an earlier attempt failed");

        var result = await _service.GetJobAsync(job.Id);

        Assert.That(result!.Error, Is.Null);
    }

    [Test]
    public async Task GetJob_AnotherUsersJob_IsNull()
    {
        var job = Seed(_scheduler.Id, JobState.Completed);
        _actors.Actor = Actor.For(_other, ActorScope.Unrestricted);

        var result = await _service.GetJobAsync(job.Id);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetJob_NoSuchJob_IsNull()
    {
        var result = await _service.GetJobAsync(Guid.NewGuid());

        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetJob_Anonymous_ThrowsNoCurrentUser()
    {
        var job = Seed(_scheduler.Id, JobState.Completed);
        _actors.Actor = Actor.Anonymous;

        Assert.Multiple(() =>
        {
            Assert.That(() => _service.GetJobAsync(job.Id), Throws.TypeOf<NoCurrentUserException>(), "an existing job");
            Assert.That(() => _service.GetJobAsync(Guid.NewGuid()), Throws.TypeOf<NoCurrentUserException>(), "no such job");
        });
    }

    [Test]
    public async Task GetJob_System_SeesAnyonesJob()
    {
        var job = Seed(_scheduler.Id, JobState.Completed);
        _actors.Actor = Actor.System;

        var result = await _service.GetJobAsync(job.Id);

        Assert.That(result?.Id, Is.EqualTo(job.Id));
    }

    private Job Seed(Guid? userId, JobState state, params (JobEntityType Type, Guid Id)[] affected) =>
        Seed(userId, state, null, affected);

    private Job Seed(Guid? userId, JobState state, string? error, params (JobEntityType Type, Guid Id)[] affected)
    {
        using var seed = NewContext();
        var job = new Job
        {
            UserId = userId,
            State = state,
            Error = error,
            MessageType = "Test.Message",
            Message = "{}",
        };
        foreach (var (type, id) in affected)
        {
            job.AffectedEntities.Add(new JobAffectedEntity { JobId = job.Id, EntityType = type, EntityId = id });
        }

        seed.Jobs.Add(job);
        seed.SaveChanges();
        return job;
    }

    private PacmanManagerDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options;
        return new PacmanManagerDbContext(options);
    }
}

using DotNet.Testcontainers.Networks;
using Microsoft.EntityFrameworkCore;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Test.Containers;

namespace PacmanManager.RepoHost.Test.Entities;

/// <summary>
/// The <see cref="Job"/> tables as the migrations build them in Postgres: the message survives the
/// <c>jsonb</c> column, and deleting a job deletes its affected entities while an affected entity
/// survives the deletion of what it names.
/// </summary>
[TestFixture]
public class JobPostgresTests
{
    private record TestCommand(Guid RepositoryId, string PackageFilePath);

    private DatabaseContainer _database = null!;
    private INetwork _network = null!;
    private DbContextOptions<PacmanManagerDbContext> _dbContextOptions = null!;
    private PacmanManagerDbContext _dbContext = null!;
    private User _user = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _network = TestNetworks.Build();
        _database = new DatabaseContainer(_network);
        await _database.StartAsync(await TestImages.Migrations.GetAsync());
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _database.DisposeAsync();
        await _network.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        _dbContextOptions = new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseNpgsql(_database.LocalConnectionString)
            .Options;

        _dbContext = new PacmanManagerDbContext(_dbContextOptions);
        _user = _dbContext.Add(new User { DisplayName = "tester", NormalizedDisplayName = "tester", Email = "test@test.com" }).Entity;
        await _dbContext.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.Jobs.ExecuteDeleteAsync();
        await _dbContext.PacmanRepositories.ExecuteDeleteAsync();
        await _dbContext.Users.ExecuteDeleteAsync();
        await _dbContext.DisposeAsync();
    }

    [Test]
    public async Task Job_RoundTripsThroughTheTables()
    {
        // Arrange
        var message = new TestCommand(Guid.CreateVersion7(), "/data/uploads/a");
        var job = Job.For(message, _user.Id);
        job.AffectedEntities.Add(new JobAffectedEntity { EntityType = JobEntityType.Repository, EntityId = message.RepositoryId });
        _dbContext.Add(job);
        await _dbContext.SaveChangesAsync();

        // Act
        await using var fresh = new PacmanManagerDbContext(_dbContextOptions);
        var stored = await fresh.Jobs
            .Include(j => j.User)
            .Include(j => j.AffectedEntities)
            .SingleAsync(j => j.Id == job.Id);
        var typed = stored.As<TestCommand>();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(stored.State, Is.EqualTo(JobState.Pending));
            Assert.That(stored.CreatedAt, Is.EqualTo(job.CreatedAt).Within(TimeSpan.FromMilliseconds(1)));
            Assert.That(typed.Message, Is.EqualTo(message));
            Assert.That(typed.User?.Id, Is.EqualTo(_user.Id));
            Assert.That(stored.AffectedEntities.Select(e => (e.EntityType, e.EntityId)),
                Is.EqualTo(new[] { (JobEntityType.Repository, message.RepositoryId) }));
        });
    }

    [Test]
    public async Task Job_WithoutAUser_IsStored()
    {
        // Arrange
        var job = Job.For(new TestCommand(Guid.CreateVersion7(), "/data/uploads/a"), null);
        _dbContext.Add(job);

        // Act
        await _dbContext.SaveChangesAsync();

        // Assert
        await using var fresh = new PacmanManagerDbContext(_dbContextOptions);
        Assert.That((await fresh.Jobs.SingleAsync(j => j.Id == job.Id)).UserId, Is.Null);
    }

    [Test]
    public async Task DeletingAJob_DeletesItsAffectedEntities()
    {
        // Arrange
        var job = Job.For(new TestCommand(Guid.CreateVersion7(), "/data/uploads/a"), _user.Id);
        job.AffectedEntities.Add(new JobAffectedEntity { EntityType = JobEntityType.Repository, EntityId = Guid.CreateVersion7() });
        job.AffectedEntities.Add(new JobAffectedEntity { EntityType = JobEntityType.Package, EntityId = Guid.CreateVersion7() });
        _dbContext.Add(job);
        await _dbContext.SaveChangesAsync();

        // Act: deleted in the database, not by the change tracker, so the cascade is the schema's.
        await _dbContext.Jobs.Where(j => j.Id == job.Id).ExecuteDeleteAsync();

        // Assert
        Assert.That(await _dbContext.JobAffectedEntities.CountAsync(e => e.JobId == job.Id), Is.Zero);
    }

    [Test]
    public async Task AffectedEntity_SurvivesTheDeletionOfWhatItNames()
    {
        // Arrange
        var repository = _dbContext.Add(new PacmanRepository
        {
            Name = "job-target",
            SupportedArchitectures = [Architectures.X86_64],
            Owner = _user,
        }).Entity;
        await _dbContext.SaveChangesAsync();
        var job = Job.For(new TestCommand(repository.Id, "/data/uploads/a"), _user.Id);
        job.AffectedEntities.Add(new JobAffectedEntity { EntityType = JobEntityType.Repository, EntityId = repository.Id });
        _dbContext.Add(job);
        await _dbContext.SaveChangesAsync();

        // Act
        await _dbContext.PacmanRepositories.Where(r => r.Id == repository.Id).ExecuteDeleteAsync();

        // Assert
        Assert.That(await _dbContext.JobAffectedEntities.CountAsync(e => e.EntityId == repository.Id), Is.EqualTo(1));
    }
}

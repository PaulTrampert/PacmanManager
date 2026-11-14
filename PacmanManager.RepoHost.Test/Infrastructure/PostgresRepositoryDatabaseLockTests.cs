using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Npgsql;
using PacmanManager.RepoHost.Infrastructure;

namespace PacmanManager.RepoHost.Test.Infrastructure;

/// <summary>
/// Tests for <see cref="PostgresRepositoryDatabaseLock"/> against a real Postgres.
/// </summary>
/// <remarks>
/// Each test builds two lock instances over the same database, as two processes would, so that what
/// excludes is Postgres and not anything one instance shares in memory.
/// </remarks>
[TestFixture]
public class PostgresRepositoryDatabaseLockTests
{
    private const string Username = "pacmanmanager";
    private const string Password = "password";

    /// <summary>How long a wait that should not end is given before it counts as blocked.</summary>
    private static readonly TimeSpan BlockedFor = TimeSpan.FromMilliseconds(500);

    /// <summary>How long a wait that should end is given before the test fails.</summary>
    private static readonly TimeSpan GrantedWithin = TimeSpan.FromSeconds(10);

    private IContainer _postgres = null!;
    private string _connectionString = null!;
    private PostgresRepositoryDatabaseLock _first = null!;
    private PostgresRepositoryDatabaseLock _second = null!;

    [OneTimeSetUp]
    public async Task StartPostgres()
    {
        _postgres = new ContainerBuilder(new DockerImage("postgres:18"))
            .WithEnvironment("POSTGRES_USER", Username)
            .WithEnvironment("POSTGRES_PASSWORD", Password)
            .WithPortBinding(5432, true)
            // pg_isready over TCP answers only once the final server is up, not the one initdb runs.
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-h", "127.0.0.1", "-U", Username))
            .WithCleanUp(true)
            .Build();
        await _postgres.StartAsync();
        _connectionString =
            $"Host=localhost;Port={_postgres.GetMappedPublicPort(5432)};Username={Username};Password={Password}";
    }

    [OneTimeTearDown]
    public async Task StopPostgres()
    {
        await _postgres.DisposeAsync();
    }

    [SetUp]
    public void SetUp()
    {
        _first = new PostgresRepositoryDatabaseLock(_connectionString);
        _second = new PostgresRepositoryDatabaseLock(_connectionString);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _first.DisposeAsync();
        await _second.DisposeAsync();
    }

    [Test]
    public async Task AcquireAsync_OnTheSameRepository_WaitsUntilTheOtherInstanceReleases()
    {
        // Arrange
        var repositoryId = Guid.CreateVersion7();
        var held = await _first.AcquireAsync(repositoryId);

        // Act
        var waiting = _second.AcquireAsync(repositoryId);
        var blocked = await Task.WhenAny(waiting, Task.Delay(BlockedFor)) != waiting;
        held.Dispose();

        // Assert
        Assert.That(blocked, Is.True, "The second instance took a lock the first still held.");
        Assert.That(await Task.WhenAny(waiting, Task.Delay(GrantedWithin)), Is.SameAs(waiting),
            "The second instance was never granted the lock the first released.");
        (await waiting).Dispose();
    }

    [Test]
    public async Task AcquireAsync_OnDifferentRepositories_DoesNotWait()
    {
        // Arrange
        var firstRepository = Guid.CreateVersion7();
        var secondRepository = Guid.CreateVersion7();
        Assume.That(PostgresRepositoryDatabaseLock.DeriveRepositoryKey(firstRepository),
            Is.Not.EqualTo(PostgresRepositoryDatabaseLock.DeriveRepositoryKey(secondRepository)));
        using var held = await _first.AcquireAsync(firstRepository);

        // Act
        var acquiring = _second.AcquireAsync(secondRepository);

        // Assert
        Assert.That(await Task.WhenAny(acquiring, Task.Delay(GrantedWithin)), Is.SameAs(acquiring),
            "A lock on one repository blocked a lock on another.");
        (await acquiring).Dispose();
    }

    [Test]
    public async Task AcquireAsync_WhenCancelledWhileWaiting_GivesUpWithoutTakingTheLock()
    {
        // Arrange
        var repositoryId = Guid.CreateVersion7();
        var held = await _first.AcquireAsync(repositoryId);
        using var cancellation = new CancellationTokenSource(BlockedFor);

        // Act
        var cancelled = _second.AcquireAsync(repositoryId, cancellation.Token);

        // Assert
        Assert.That(async () => await cancelled, Throws.InstanceOf<OperationCanceledException>());
        held.Dispose();
        Assert.That(await AdvisoryLockHoldersAsync(repositoryId), Is.Empty,
            "The cancelled wait still holds, or still waits for, the lock.");
        var next = _first.AcquireAsync(repositoryId);
        Assert.That(await Task.WhenAny(next, Task.Delay(GrantedWithin)), Is.SameAs(next),
            "The lock was not free after the only holder released it and the only waiter gave up.");
        (await next).Dispose();
    }

    [Test]
    public async Task AcquireAsync_WhenTheHoldersConnectionIsClosed_IsGranted()
    {
        // Arrange: hold the lock, then end its session from outside, as Postgres does when the
        // process holding it dies.
        var repositoryId = Guid.CreateVersion7();
        var held = await _first.AcquireAsync(repositoryId);
        var holders = await AdvisoryLockHoldersAsync(repositoryId);
        Assume.That(holders, Has.Count.EqualTo(1));
        var waiting = _second.AcquireAsync(repositoryId);
        Assume.That(await Task.WhenAny(waiting, Task.Delay(BlockedFor)), Is.Not.SameAs(waiting));

        // Act
        await TerminateBackendAsync(holders[0]);

        // Assert
        Assert.That(await Task.WhenAny(waiting, Task.Delay(GrantedWithin)), Is.SameAs(waiting),
            "The lock outlived the connection that held it.");
        (await waiting).Dispose();
        Assert.That(held.Dispose, Throws.Nothing,
            "Releasing a lock whose connection is already gone should only close what is left of it.");
    }

    [Test]
    public async Task Dispose_ReleasesTheLockAndClosesItsConnection()
    {
        // Arrange
        var repositoryId = Guid.CreateVersion7();
        var held = await _first.AcquireAsync(repositoryId);
        var holders = await AdvisoryLockHoldersAsync(repositoryId);
        Assume.That(holders, Has.Count.EqualTo(1));

        // Act
        held.Dispose();
        held.Dispose();

        // Assert
        Assert.Multiple(async () =>
        {
            Assert.That(await AdvisoryLockHoldersAsync(repositoryId), Is.Empty);
            Assert.That(await IsBackendAliveAsync(holders[0]), Is.False,
                "Disposing the handle left its connection open.");
        });
    }

    /// <summary>
    /// The backends holding or waiting for the repository database lock on one repository.
    /// </summary>
    private async Task<List<int>> AdvisoryLockHoldersAsync(Guid repositoryId)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        // A two-key advisory lock shows its keys as classid and objid, with objsubid 2. They are
        // oids, so the signed keys are compared as the unsigned values they are stored as.
        await using var command = new NpgsqlCommand(
            """
            SELECT pid FROM pg_locks
            WHERE locktype = 'advisory' AND objsubid = 2
              AND classid::bigint = $1 AND objid::bigint = $2
            """, connection);
        command.Parameters.Add(new NpgsqlParameter<long>
            { TypedValue = (uint)PostgresRepositoryDatabaseLock.RepositoryDatabasesKey });
        command.Parameters.Add(new NpgsqlParameter<long>
            { TypedValue = (uint)PostgresRepositoryDatabaseLock.DeriveRepositoryKey(repositoryId) });
        var pids = new List<int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            pids.Add(reader.GetInt32(0));
        }

        return pids;
    }

    private async Task TerminateBackendAsync(int pid)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT pg_terminate_backend($1)", connection);
        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = pid });
        await command.ExecuteNonQueryAsync();
    }

    private async Task<bool> IsBackendAliveAsync(int pid)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        // A closed connection's backend may take a moment to leave pg_stat_activity.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            await using var command = new NpgsqlCommand(
                "SELECT count(*) FROM pg_stat_activity WHERE pid = $1", connection);
            command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = pid });
            if ((long)(await command.ExecuteScalarAsync())! == 0)
            {
                return false;
            }

            await Task.Delay(100);
        }

        return true;
    }
}

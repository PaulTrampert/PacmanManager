using System.Text;
using DotNet.Testcontainers.Networks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Config;
using PacmanManager.RepoHost.Test.Containers;
using PacmanManager.TestUtils;
using RabbitMQ.Client;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.RabbitMQ;

namespace PacmanManager.RepoHost.Test.Config;

/// <summary>
/// Wolverine as <see cref="MessagingServiceCollectionExtensions.AddMessaging"/> configures it,
/// against a real Postgres and RabbitMQ: a message published through the EF Core outbox reaches the
/// broker only once the context's changes have committed, and never when they roll back.
/// </summary>
/// <remarks>
/// The broker is read directly with the RabbitMQ client rather than by a Wolverine listener, so that
/// what is asserted is what actually reached the queue.
/// </remarks>
[TestFixture]
public class MessagingOutboxTests
{
    /// <summary>A message routed only by this fixture.</summary>
    public record OutboxTestMessage(Guid Id);

    private const string QueueName = "pacmanmanager-test-outbox";

    /// <summary>How long a message that should arrive is given before the test fails.</summary>
    private static readonly TimeSpan ArrivesWithin = TimeSpan.FromSeconds(15);

    /// <summary>How long a message that should never arrive is waited for.</summary>
    private static readonly TimeSpan AbsentFor = TimeSpan.FromSeconds(3);

    private INetwork _network = null!;
    private DatabaseContainer _database = null!;
    private RabbitMqContainer _rabbitMq = null!;
    private IHost _host = null!;
    private IConnection _rabbitConnection = null!;
    private IChannel _channel = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _network = TestNetworks.Build();
        _database = new DatabaseContainer(_network);
        _rabbitMq = new RabbitMqContainer(_network, DirUtils.FindSolutionDirectory());
        await Task.WhenAll(_database.StartAsync(await TestImages.Migrations.GetAsync()), _rabbitMq.StartAsync());

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{ConnectionStringIds.Database}"] =
                _database.LocalConnectionString,
            [$"ConnectionStrings:{ConnectionStringIds.RabbitMq}"] =
                _rabbitMq.LocalConnectionString,
        });
        builder.Services.AddMessaging(builder.Configuration,
            opts => opts.PublishMessage<OutboxTestMessage>().ToRabbitQueue(QueueName));
        _host = builder.Build();
        await _host.StartAsync();

        _rabbitConnection = await new ConnectionFactory { Uri = new Uri(_rabbitMq.LocalConnectionString) }
            .CreateConnectionAsync();
        _channel = await _rabbitConnection.CreateChannelAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _channel.DisposeAsync();
        await _rabbitConnection.DisposeAsync();
        await _host.StopAsync();
        _host.Dispose();
        await _rabbitMq.DisposeAsync();
        await _database.DisposeAsync();
        await _network.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        await _channel.QueuePurgeAsync(QueueName);
    }

    [TearDown]
    public async Task TearDown()
    {
        await using var scope = _host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PacmanManagerDbContext>().Users.ExecuteDeleteAsync();
    }

    [Test]
    public async Task Startup_CreatesWolverinesTablesInItsOwnSchema()
    {
        // Arrange
        await using var connection = new NpgsqlConnection(_database.LocalConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "select table_name from information_schema.tables where table_schema = @schema", connection);
        command.Parameters.AddWithValue("schema", MessagingServiceCollectionExtensions.WolverineSchema);

        // Act
        var tables = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));
        }

        // Assert
        Assert.That(tables, Is.SupersetOf(new[] { "wolverine_outgoing_envelopes", "wolverine_incoming_envelopes" }));
    }

    [Test]
    public async Task SaveChangesAndFlushMessages_CommitsTheChangesAndSendsTheMessage()
    {
        // Arrange
        var message = new OutboxTestMessage(Guid.CreateVersion7());
        await using var scope = _host.Services.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<PacmanManagerDbContext>>();
        var user = outbox.DbContext.Add(NewUser()).Entity;
        await outbox.PublishAsync(message);

        // Act
        await outbox.SaveChangesAndFlushMessagesAsync();

        // Assert
        Assert.That(await ReceiveAsync(ArrivesWithin), Does.Contain(message.Id.ToString()));
        Assert.That(await UserExistsAsync(user.Id), Is.True);
    }

    [Test]
    public async Task SaveChanges_InATransactionThatCommits_SendsTheMessage()
    {
        // Arrange
        var message = new OutboxTestMessage(Guid.CreateVersion7());
        await using var scope = _host.Services.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<PacmanManagerDbContext>>();
        var user = outbox.DbContext.Add(NewUser()).Entity;

        // Act
        await using (var transaction = await outbox.DbContext.Database.BeginTransactionAsync())
        {
            await outbox.PublishAsync(message);
            await outbox.DbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        await outbox.FlushOutgoingMessagesAsync();

        // Assert
        Assert.That(await ReceiveAsync(ArrivesWithin), Does.Contain(message.Id.ToString()));
        Assert.That(await UserExistsAsync(user.Id), Is.True);
    }

    [Test]
    public async Task SaveChanges_InATransactionThatRollsBack_NeverSendsTheMessage()
    {
        // Arrange
        var message = new OutboxTestMessage(Guid.CreateVersion7());
        Guid userId = default;

        // Act: the work fails after saving and before committing, as a caller's would, so the
        // transaction is rolled back by its disposal and nothing is ever flushed.
        var failure = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var scope = _host.Services.CreateAsyncScope();
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<PacmanManagerDbContext>>();
            userId = outbox.DbContext.Add(NewUser()).Entity.Id;
            await using var transaction = await outbox.DbContext.Database.BeginTransactionAsync();
            await outbox.PublishAsync(message);
            await outbox.DbContext.SaveChangesAsync();
            throw new InvalidOperationException("The work failed before committing.");
        });

        // Assert: neither sent now, nor left in the outbox for Wolverine to recover and send later.
        Assert.That(failure, Is.Not.Null);
        Assert.That(await ReceiveAsync(AbsentFor), Is.Null);
        Assert.That(await UserExistsAsync(userId), Is.False);
        Assert.That(await CountOutgoingEnvelopesAsync(), Is.Zero);
    }

    private static User NewUser()
    {
        var name = $"outbox-{Guid.NewGuid():N}";
        return new User { DisplayName = name, NormalizedDisplayName = name.ToUpperInvariant(), Email = $"{name}@test.com" };
    }

    /// <summary>
    /// Waits up to <paramref name="within"/> for a message on the test queue.
    /// </summary>
    /// <returns>The message's body, or <c>null</c> if none arrived.</returns>
    private async Task<string?> ReceiveAsync(TimeSpan within)
    {
        var deadline = DateTime.UtcNow + within;
        do
        {
            var result = await _channel.BasicGetAsync(QueueName, autoAck: true);
            if (result is not null)
                return Encoding.UTF8.GetString(result.Body.Span);
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        } while (DateTime.UtcNow < deadline);

        return null;
    }

    private async Task<bool> UserExistsAsync(Guid id)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PacmanManagerDbContext>().Users.AnyAsync(u => u.Id == id);
    }

    private async Task<long> CountOutgoingEnvelopesAsync()
    {
        await using var connection = new NpgsqlConnection(_database.LocalConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"select count(*) from {MessagingServiceCollectionExtensions.WolverineSchema}.wolverine_outgoing_envelopes",
            connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}

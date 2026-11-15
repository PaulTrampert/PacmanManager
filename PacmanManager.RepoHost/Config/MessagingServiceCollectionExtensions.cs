using JasperFx;
using Microsoft.EntityFrameworkCore;
using PacmanManager.Entities;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace PacmanManager.RepoHost.Config;

/// <summary>
/// Registers <see cref="PacmanManagerDbContext"/> together with Wolverine, which publishes messages
/// to RabbitMQ through a transactional outbox kept in the same Postgres database.
/// </summary>
/// <remarks>
/// <para>
/// The outbox is what lets a row and the message that refers to it commit together: a message
/// published through <see cref="IDbContextOutbox{T}"/> is written to Wolverine's outgoing envelope
/// table by the same <c>SaveChangesAsync</c>, inside the same transaction, as the context's own
/// changes, and is only handed to RabbitMQ once that has committed. A transaction that rolls back
/// takes the message with it.
/// </para>
/// <para>
/// Handing the message over is the caller's last step: either
/// <see cref="IDbContextOutbox{T}.SaveChangesAndFlushMessagesAsync(CancellationToken)"/>, or, inside a
/// transaction of the caller's own, <see cref="IDbContextOutbox.FlushOutgoingMessagesAsync"/> once
/// the transaction has committed. A flush after a rollback would still send what was published, so
/// a caller whose work fails must not flush.
/// </para>
/// <para>
/// Wolverine keeps its storage tables in a schema of its own, <see cref="WolverineSchema"/>, and
/// creates or updates them when the host starts. They are deliberately not part of
/// <c>PacmanManager.Migrations</c>.
/// </para>
/// </remarks>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// The name of the connection string for the Postgres database, which holds both the
    /// application's tables and Wolverine's.
    /// </summary>
    public const string DatabaseConnectionStringName = "pacmanmanager";

    /// <summary>
    /// The name of the connection string for the RabbitMQ broker, an AMQP URI such as
    /// <c>amqp://user:password@rabbitmq:5672/</c>.
    /// </summary>
    public const string RabbitMqConnectionStringName = "rabbitmq";

    /// <summary>
    /// The Postgres schema Wolverine's message storage tables live in.
    /// </summary>
    public const string WolverineSchema = "wolverine";

    /// <summary>
    /// Registers <see cref="PacmanManagerDbContext"/>, enlisted in Wolverine's EF Core outbox, and
    /// Wolverine itself, connected to RabbitMQ with every sending endpoint durable.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">
    /// The configuration holding the <see cref="DatabaseConnectionStringName"/> and
    /// <see cref="RabbitMqConnectionStringName"/> connection strings.
    /// </param>
    /// <param name="configure">
    /// Further Wolverine configuration, applied last, such as the routes messages are published to.
    /// </param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">Either connection string is not configured.</exception>
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration,
        Action<WolverineOptions>? configure = null)
    {
        var databaseConnectionString = RequireConnectionString(configuration, DatabaseConnectionStringName);
        var rabbitMqUri = new Uri(RequireConnectionString(configuration, RabbitMqConnectionStringName));

        services.AddDbContextWithWolverineIntegration<PacmanManagerDbContext>(
            opts => opts.UseNpgsql(databaseConnectionString), WolverineSchema);

        services.AddWolverine(opts =>
        {
            // Fixed rather than taken from the process's entry assembly, which in a test host is the
            // test runner's.
            opts.ApplicationAssembly = typeof(MessagingServiceCollectionExtensions).Assembly;

            opts.PersistMessagesWithPostgresql(databaseConnectionString, WolverineSchema);
            opts.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate;
            opts.UseEntityFrameworkCoreTransactions();

            opts.UseRabbitMq(rabbitMqUri).AutoProvision();
            opts.Policies.UseDurableOutboxOnAllSendingEndpoints();

            configure?.Invoke(opts);
        });

        return services;
    }

    private static string RequireConnectionString(IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name)
        ?? throw new InvalidOperationException($"ConnectionStrings:{name} is not configured.");
}

using JasperFx;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PacmanManager.Entities;
using Serilog;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace PacmanManager.Worker;

/// <summary>
/// Configures the Worker's host, kept apart from <c>Program</c> so that tests build the same host the
/// process runs.
/// </summary>
public static class WorkerHost
{
    /// <summary>
    /// Adds the Worker's logging, its database and its messaging to <paramref name="builder"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="PacmanManagerDbContext"/> is enlisted in Wolverine's EF Core integration, with
    /// Wolverine's message storage in the same Postgres database, as RepoHost's is: the messages a
    /// handler sends are written to the outbox by the same <c>SaveChangesAsync</c> as its rows, and
    /// commit or roll back with them. Wolverine creates or updates its storage tables, in
    /// <see cref="DatabaseConfig.WolverineSchema"/>, when the host starts.
    /// </remarks>
    /// <param name="builder">The host being built.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="MissingConfigurationException">
    /// <c>ConnectionStrings:pacmanmanager</c> or <c>ConnectionStrings:rabbitmq</c> is not configured.
    /// </exception>
    public static HostApplicationBuilder ConfigureWorker(this HostApplicationBuilder builder)
    {
        builder.Services.AddSerilog((_, logger) => logger.ReadFrom.Configuration(builder.Configuration));

        var database = RequireConnectionString(builder.Configuration, DatabaseConfig.ConnectionStringName);
        var rabbitMq = RequireConnectionString(builder.Configuration, MessagingConfig.ConnectionStringName);

        var messaging = builder.Configuration.GetSection(MessagingConfig.Section).Get<MessagingConfig>()
                        ?? new MessagingConfig();

        builder.Services.AddDbContextWithWolverineIntegration<PacmanManagerDbContext>(
            opts => opts.UseNpgsql(database), DatabaseConfig.WolverineSchema);

        builder.UseWolverine(opts =>
        {
            // Handlers are discovered in this assembly, whichever assembly started the process: a
            // test host's entry assembly is the test runner's.
            opts.ApplicationAssembly = typeof(WorkerHost).Assembly;

            opts.PersistMessagesWithPostgresql(database, DatabaseConfig.WolverineSchema);
            opts.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate;
            opts.UseEntityFrameworkCoreTransactions();

            opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();
            opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
            opts.ListenToRabbitQueue(messaging.QueueName);
        });

        return builder;
    }

    private static string RequireConnectionString(IConfiguration configuration, string name)
    {
        var value = configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new MissingConfigurationException($"ConnectionStrings:{name}");
        }

        return value;
    }
}

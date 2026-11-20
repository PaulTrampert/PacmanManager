using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Wolverine;
using Wolverine.RabbitMQ;

namespace PacmanManager.Worker;

/// <summary>
/// Configures the Worker's host, kept apart from <c>Program</c> so that tests build the same host the
/// process runs.
/// </summary>
public static class WorkerHost
{
    /// <summary>
    /// Adds the Worker's logging and messaging to <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The host being built.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="MissingConfigurationException">
    /// <c>ConnectionStrings:rabbitmq</c> is not configured.
    /// </exception>
    public static HostApplicationBuilder ConfigureWorker(this HostApplicationBuilder builder)
    {
        builder.Services.AddSerilog((_, logger) => logger.ReadFrom.Configuration(builder.Configuration));

        var rabbitMq = builder.Configuration.GetConnectionString(MessagingConfig.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(rabbitMq))
        {
            throw new MissingConfigurationException($"ConnectionStrings:{MessagingConfig.ConnectionStringName}");
        }

        var messaging = builder.Configuration.GetSection(MessagingConfig.Section).Get<MessagingConfig>()
                        ?? new MessagingConfig();

        builder.UseWolverine(opts =>
        {
            // Handlers are discovered in this assembly, whichever assembly started the process: a
            // test host's entry assembly is the test runner's.
            opts.ApplicationAssembly = typeof(WorkerHost).Assembly;

            opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();
            opts.ListenToRabbitQueue(messaging.QueueName);
        });

        return builder;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PacmanManager.Entities;
using Wolverine;
using Wolverine.Tracking;

namespace PacmanManager.Worker.Test;

public class WorkerHostTests
{
    private const string RabbitMq = "amqp://pacmanmanager.worker:password@rabbitmq:5672";
    private const string Database = "Server=postgres;Database=pacmanmanager;User Id=pacmanmanager;Password=password;";

    private static HostApplicationBuilder CreateBuilder(IDictionary<string, string?> settings)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(settings);
        return builder;
    }

    private static async Task<IHost> StartWorkerAsync(IDictionary<string, string?> settings)
    {
        var builder = CreateBuilder(settings).ConfigureWorker();
        // Neither the broker nor Postgres is running here: the endpoints and message storage are
        // configured as in production, but nothing connects to them.
        builder.Services.DisableAllExternalWolverineTransports();
        builder.Services.DisableAllWolverineMessagePersistence();
        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private static IEnumerable<Uri> ListenerUris(IHost host) =>
        host.GetRuntime().Options.Transports.AllEndpoints()
            .Where(e => e.IsListener)
            .Select(e => e.Uri);

    [Test]
    public async Task ConfigureWorker_ListensOnTheDefaultRabbitMqQueue()
    {
        using var host = await StartWorkerAsync(new Dictionary<string, string?>
        {
            ["ConnectionStrings:pacmanmanager"] = Database,
            ["ConnectionStrings:rabbitmq"] = RabbitMq,
        });

        Assert.That(ListenerUris(host),
            Does.Contain(new Uri($"rabbitmq://queue/{MessagingConfig.DefaultQueueName}")));
        await host.StopAsync();
    }

    [Test]
    public async Task ConfigureWorker_ListensOnTheConfiguredRabbitMqQueue()
    {
        using var host = await StartWorkerAsync(new Dictionary<string, string?>
        {
            ["ConnectionStrings:pacmanmanager"] = Database,
            ["ConnectionStrings:rabbitmq"] = RabbitMq,
            ["Messaging:QueueName"] = "some.other.queue",
        });

        Assert.That(ListenerUris(host), Does.Contain(new Uri("rabbitmq://queue/some.other.queue")));
        Assert.That(ListenerUris(host),
            Does.Not.Contain(new Uri($"rabbitmq://queue/{MessagingConfig.DefaultQueueName}")));
        await host.StopAsync();
    }

    [Test]
    public async Task ConfigureWorker_RegistersPacmanManagerDbContextAgainstTheConfiguredDatabase()
    {
        using var host = await StartWorkerAsync(new Dictionary<string, string?>
        {
            ["ConnectionStrings:pacmanmanager"] = Database,
            ["ConnectionStrings:rabbitmq"] = RabbitMq,
        });

        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PacmanManagerDbContext>();

            Assert.Multiple(() =>
            {
                Assert.That(context.Database.ProviderName, Is.EqualTo("Npgsql.EntityFrameworkCore.PostgreSQL"));
                Assert.That(context.Database.GetConnectionString(), Does.Contain("Database=pacmanmanager"));
            });
        }

        await host.StopAsync();
    }

    [Test]
    public void ConfigureWorker_WithoutADatabaseConnectionString_Throws()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["ConnectionStrings:rabbitmq"] = RabbitMq,
        });

        var ex = Assert.Throws<MissingConfigurationException>(() => builder.ConfigureWorker());
        Assert.That(ex.Key, Is.EqualTo("ConnectionStrings:pacmanmanager"));
    }

    [Test]
    public void ConfigureWorker_WithoutARabbitMqConnectionString_Throws()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["ConnectionStrings:pacmanmanager"] = Database,
        });

        var ex = Assert.Throws<MissingConfigurationException>(() => builder.ConfigureWorker());
        Assert.That(ex.Key, Is.EqualTo("ConnectionStrings:rabbitmq"));
    }
}

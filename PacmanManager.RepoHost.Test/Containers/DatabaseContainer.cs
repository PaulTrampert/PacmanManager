using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PacmanManager.Entities;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Containers;

public class DatabaseContainer(INetwork network, string? hostname = null) : IAsyncDisposable
{
    private const string DefaultHostname = "postgres-db";
 
    private readonly ILogger _logger = new TestOutputLogger(nameof(DatabaseContainer));
    
    private readonly IContainer _container = new ContainerBuilder(new DockerImage("postgres:18"))
        .WithNetwork(network)
        .WithNetworkAliases(hostname ?? DefaultHostname)
        .WithEnvironment("POSTGRES_USER", Username)
        .WithEnvironment("POSTGRES_PASSWORD", Password)
        .WithPortBinding(5432, true)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilInternalTcpPortIsAvailable(5432))
        .WithOutputConsumer(Consume.RedirectStdoutAndStderrToConsole())
        .WithCleanUp(true)
        .Build();
    
    public string Hostname => hostname ?? DefaultHostname;

    public const string Username = "pacmanmanager";
    public const string Password = "password";
    public string ConnectionString => $"Server={Hostname};User Id={Username};Password={Password};";

    public async Task StartAsync(IImage migrationsImage)
    {
        await _container.StartAsync();

        await using var migrationsContainer = new ContainerBuilder(migrationsImage)
            .WithNetwork(network)
            .WithEnvironment("ConnectionStrings__pacmanmanager", ConnectionString)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .AddCustomWaitStrategy(new UntilExitWaitStrategy(), s => s.WithMode(WaitStrategyMode.OneShot)))
            .WithOutputConsumer(Consume.RedirectStdoutAndStderrToConsole())
            .WithCleanUp(true)
            .Build();
        
        await migrationsContainer.StartAsync();
        
        var migrationsExitCode = await migrationsContainer.GetExitCodeAsync();
        if (migrationsExitCode != 0)
        {
            var logs = await migrationsContainer.GetLogsAsync();
            _logger.LogError("Migrations container failed with exit code {ExitCode}. Logs:\n{Logs}", migrationsExitCode, logs);
            throw new Exception($"Migrations container failed with exit code {migrationsExitCode}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if ((_container.State & (TestcontainersStates.Dead | TestcontainersStates.Exited)) == 0)
        {
            await _container.StopAsync();
        }
        await _container.DisposeAsync();
    }
}
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;

namespace PacmanManager.RepoHost.Test.Containers;

/// <summary>
/// A RabbitMQ broker configured as the compose stack's is, from <c>rabbitmq/rabbitmq.conf</c> and
/// <c>rabbitmq/definitions.json</c>, so that RepoHost connects as the same user it does there.
/// </summary>
/// <param name="network">The network the broker joins.</param>
/// <param name="solutionRoot">The solution directory, which holds the <c>rabbitmq</c> folder.</param>
/// <param name="hostname">The network alias the broker answers to, or <c>null</c> for the default.</param>
public class RabbitMqContainer(INetwork network, string solutionRoot, string? hostname = null) : IAsyncDisposable
{
    private const string DefaultHostname = "rabbitmq";
    private const int AmqpPort = 5672;

    /// <summary>The user RepoHost connects as, from <c>rabbitmq/definitions.json</c>.</summary>
    public const string RepoHostUsername = "pacmanmanager.repohost";

    /// <summary>The password of <see cref="RepoHostUsername"/>.</summary>
    public const string RepoHostPassword = "password";

    private readonly IContainer _container = new ContainerBuilder(new DockerImage("rabbitmq:4-management"))
        .WithNetwork(network)
        .WithNetworkAliases(hostname ?? DefaultHostname)
        .WithBindMount(Path.Combine(solutionRoot, "rabbitmq/rabbitmq.conf"),
            "/etc/rabbitmq/conf.d/20-pacmanmanager.conf", AccessMode.ReadOnly)
        .WithBindMount(Path.Combine(solutionRoot, "rabbitmq/definitions.json"),
            "/etc/rabbitmq/definitions.json", AccessMode.ReadOnly)
        .WithPortBinding(AmqpPort, true)
        // Logged once the definitions, and so the users, are loaded and the listeners are up. Not
        // rabbitmq-diagnostics: an exec runs as root, and one that wins the race to create the Erlang
        // cookie leaves the broker, which runs as rabbitmq, unable to read it.
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server startup complete"))
        .WithOutputConsumer(Consume.RedirectStdoutAndStderrToConsole())
        .WithCleanUp(true)
        .Build();

    /// <summary>The network alias the broker answers to.</summary>
    public string Hostname => hostname ?? DefaultHostname;

    /// <summary>The AMQP URI another container on the network reaches the broker at, as RepoHost.</summary>
    public string ConnectionString => $"amqp://{RepoHostUsername}:{RepoHostPassword}@{Hostname}:{AmqpPort}/";

    /// <summary>The AMQP URI the test host reaches the broker at, as RepoHost.</summary>
    public string LocalConnectionString =>
        $"amqp://{RepoHostUsername}:{RepoHostPassword}@localhost:{_container.GetMappedPublicPort(AmqpPort)}/";

    /// <summary>Starts the broker and waits until it accepts connections.</summary>
    public Task StartAsync() => _container.StartAsync();

    /// <summary>Stops and removes the broker.</summary>
    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

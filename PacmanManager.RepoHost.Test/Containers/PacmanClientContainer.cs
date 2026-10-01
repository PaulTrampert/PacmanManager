using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Containers;

/// <summary>
/// A stock Arch Linux container, used as a real <c>pacman</c> client of the API on the same network.
/// </summary>
/// <param name="network">The network the API is reachable on.</param>
/// <remarks>
/// The container does nothing on its own; it idles until it is disposed, and each step a test takes
/// is a command run inside it with <see cref="ExecAsync"/>. It is the same image the RepoHost image
/// is built from and the <c>libalpm</c> CI job runs in, so it adds no image a run does not already
/// pull.
/// </remarks>
public sealed class PacmanClientContainer(INetwork network) : IAsyncDisposable
{
    /// <summary>
    /// Where <c>pacman</c> reads its configuration from by default.
    /// </summary>
    public const string PacmanConfPath = "/etc/pacman.conf";

    private readonly IContainer _container = new ContainerBuilder(new DockerImage("archlinux/archlinux"))
        .WithNetwork(network)
        .WithEntrypoint("sleep", "infinity")
        .WithOutputConsumer(Consume.RedirectStdoutAndStderrToConsole())
        .WithLogger(new TestOutputLogger(nameof(PacmanClientContainer)))
        .WithCleanUp(true)
        .Build();

    /// <summary>
    /// Starts the container.
    /// </summary>
    /// <returns>A task that completes once the container is running.</returns>
    public Task StartAsync() => _container.StartAsync();

    /// <summary>
    /// Replaces the container's <c>pacman.conf</c>.
    /// </summary>
    /// <param name="contents">The whole configuration file.</param>
    /// <returns>A task that completes once the file is written.</returns>
    public Task WritePacmanConfAsync(string contents) =>
        _container.CopyAsync(Encoding.UTF8.GetBytes(contents), PacmanConfPath);

    /// <summary>
    /// Runs a command inside the container.
    /// </summary>
    /// <param name="command">The command and its arguments.</param>
    /// <returns>The command's exit code and output, whether or not it succeeded.</returns>
    public Task<ExecResult> ExecAsync(params string[] command) => _container.ExecAsync(command);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

using LibAlpmSharp.Config;
using PacmanManager.RepoHost.Infrastructure;

namespace PacmanManager.RepoHost.Startup.LibAlpm;

/// <summary>
/// Creates the <c>sync</c> and <c>local</c> directories libalpm expects under the generated pacman
/// configuration's database path, once, when the host starts.
/// </summary>
/// <remarks>
/// This is a hosted service rather than code after <c>builder.Build()</c> in <c>Program.cs</c>
/// because the Swashbuckle CLI runs the whole of <c>Program.cs</c> to generate the OpenAPI document,
/// with a server that never listens, but removes every hosted service first. Startup work that needs
/// <c>DATA_DIR</c> therefore never runs during generation, which needs neither a data directory nor
/// a database.
/// </remarks>
/// <param name="pacmanConfig">The pacman configuration generated for <c>DATA_DIR</c>.</param>
/// <param name="fileSystem">The file system the directories are created on.</param>
public class PacmanDirectoriesInitializer(PacmanConfig pacmanConfig, IFileSystem fileSystem) : IHostedService
{
    /// <summary>
    /// Creates the directories. Directories that already exist are left as they are.
    /// </summary>
    /// <param name="cancellationToken">Unused; the work is synchronous and brief.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        fileSystem.CreateDirectory(Path.Combine(pacmanConfig.DBPath, "sync"));
        fileSystem.CreateDirectory(Path.Combine(pacmanConfig.DBPath, "local"));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Does nothing; the directories outlive the host.
    /// </summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

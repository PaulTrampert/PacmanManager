using PacmanManager.CliTools;
using PacmanManager.RepoHost.Exceptions;

namespace PacmanManager.RepoHost.Infrastructure;

/// <summary>
/// The <see cref="IRepositoryDatabaseToolRunner"/> that actually runs the tool: an
/// <see cref="ICliToolRunner"/> plus the exit code check every caller would otherwise have to
/// remember.
/// </summary>
/// <remarks>
/// <para>
/// Every failure is the same failure: the repository database did not change, and what the caller
/// can do about it does not depend on why. The tools distinguish their reasons only in prose —
/// there is no exit code for a lock file it could not take — so guessing at the reason from the
/// message would only be right some of the time, and a wrong guess (a package whose name happens to
/// contain the word) would advise a client to retry something that will never succeed. So there is
/// one exception, <see cref="RepositoryDatabaseToolException"/>, which the exception handlers
/// deliberately leave unmapped: the request was fine and something on the server was not.
/// </para>
/// <para>
/// The output is collected rather than only logged by the global handlers, because the exception
/// carries the diagnostics too — a stack trace that says a tool exited 1 and nothing else is a poor
/// thing to find in a log.
/// </para>
/// </remarks>
/// <param name="cliRunner">The generic runner the tool is executed with.</param>
/// <param name="logger">Where the tool's diagnostics are recorded for an operator.</param>
internal sealed class RepositoryDatabaseToolRunner(
    ICliToolRunner cliRunner,
    ILogger<RepositoryDatabaseToolRunner> logger) : IRepositoryDatabaseToolRunner
{
    /// <inheritdoc />
    public async Task RunAsync(ICliTool tool, Guid repositoryId, CancellationToken cancellationToken = default)
    {
        var output = new CollectingCliOutputHandler();
        var exitCode = await cliRunner.RunToolAsync(tool, output, cancellationToken);
        if (exitCode == 0)
        {
            return;
        }

        var diagnostics = string.IsNullOrWhiteSpace(output.StdErr) ? output.StdOut : output.StdErr;
        logger.LogError(
            "'{Tool}' exited with code {ExitCode} against repository {RepositoryId}: {Diagnostics}",
            tool.Name, exitCode, repositoryId, diagnostics);

        throw new RepositoryDatabaseToolException(tool.Name, exitCode, diagnostics);
    }
}

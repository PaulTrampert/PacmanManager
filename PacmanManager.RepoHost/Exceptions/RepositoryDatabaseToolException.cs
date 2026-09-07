namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Thrown when one of the pacman database tools exited non-zero for a reason that is not a lock
/// file conflict.
/// </summary>
/// <remarks>
/// <c>ICliToolRunner</c> reports an exit code rather than throwing, so every caller that cares
/// whether the repository database actually changed has to check it. This is what "it did not"
/// looks like, and it is deliberately left unmapped by the exception handlers: the request was
/// fine and something on the server was not.
/// </remarks>
/// <param name="tool">The tool that failed, e.g. <c>repo-add</c>.</param>
/// <param name="exitCode">The exit code it reported.</param>
/// <param name="standardError">Whatever it wrote to standard error.</param>
public class RepositoryDatabaseToolException(string tool, int exitCode, string standardError)
    : Exception($"'{tool}' exited with code {exitCode}: {standardError}")
{
    /// <summary>
    /// The tool that failed.
    /// </summary>
    public string Tool { get; } = tool;

    /// <summary>
    /// The exit code the tool reported.
    /// </summary>
    public int ExitCode { get; } = exitCode;

    /// <summary>
    /// Whatever the tool wrote to standard error.
    /// </summary>
    public string StandardError { get; } = standardError;
}

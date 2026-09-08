using PacmanManager.CliTools;
using PacmanManager.RepoHost.Exceptions;

namespace PacmanManager.RepoHost.Infrastructure;

/// <summary>
/// Runs one of the pacman database tools against a repository's <c>.db.tar.gz</c> and turns a
/// non-zero exit into an exception.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ICliToolRunner"/> reports a tool that ran and failed only through its return value —
/// it throws when the process cannot be <i>started</i>, and never for what the process then did.
/// That is the right contract for a generic runner and the wrong default for a caller, because
/// ignoring a failure is spelled the same way as not caring about the result. Every caller that
/// rewrites a repository database therefore goes through this instead, where the check is the only
/// thing on offer.
/// </para>
/// <para>
/// This is a collaborator both <c>RepositoryService</c> and <c>PackageService</c> take rather than
/// a base class they share, so that each stays constructible and mockable on its own, and so that
/// running a repository database tool sits with the other repository database concerns —
/// <see cref="IRepositoryDatabaseLock"/> next door — rather than inside whichever service happened
/// to need it first.
/// </para>
/// </remarks>
internal interface IRepositoryDatabaseToolRunner
{
    /// <summary>
    /// Runs <paramref name="tool"/> to completion, succeeding only on a zero exit code.
    /// </summary>
    /// <param name="tool">The tool to run, e.g. <c>repo-add</c> or <c>repo-remove</c>.</param>
    /// <param name="repositoryId">
    /// The repository whose database is being rewritten. Used to name the repository in the
    /// diagnostics an operator will read; the tool itself already knows which file it is opening.
    /// </param>
    /// <param name="cancellationToken">A token to abandon the run.</param>
    /// <exception cref="RepositoryDatabaseToolException">The tool exited non-zero.</exception>
    /// <exception cref="InvalidOperationException">The tool could not be started at all.</exception>
    Task RunAsync(ICliTool tool, Guid repositoryId, CancellationToken cancellationToken = default);
}

namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Thrown when <c>repo-add</c> or <c>repo-remove</c> could not take the lock file beside a
/// repository's database, because another writer holds it.
/// </summary>
/// <remarks>
/// This is a transient condition rather than a fault: the caller's request changed nothing, and
/// retrying it is the right response. Mutations are serialized per repository in-process, so a
/// lock file conflict means a writer outside this process — another instance, or someone running
/// the tools by hand against the same data directory.
/// </remarks>
/// <param name="repositoryId">The repository whose database was locked.</param>
public class RepositoryDatabaseLockedException(Guid repositoryId)
    : Exception($"The database for repository '{repositoryId}' is locked by another writer. Try again.")
{
    /// <summary>
    /// The repository whose database was locked.
    /// </summary>
    public Guid RepositoryId { get; } = repositoryId;
}

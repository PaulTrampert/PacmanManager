namespace PacmanManager.RepoHost.Infrastructure;

/// <summary>
/// Serializes every mutation of a repository's <c>.db.tar.gz</c>, one repository at a time.
/// </summary>
/// <remarks>
/// <para>
/// <c>repo-add</c> and <c>repo-remove</c> both rewrite the same file and both take the same lock
/// file beside it, failing rather than corrupting it. That is a safe floor and a poor experience,
/// so publish and delete take this lock first and only meet each other's lock file when a writer
/// outside this process is involved.
/// </para>
/// <para>
/// Publish and delete share one lock deliberately. A lock that only covered publishing would leave
/// exactly the race it was added to prevent, because <c>repo-remove</c> mutates the file
/// <c>repo-add</c> is writing.
/// </para>
/// <para>
/// The lock is held across the side effects <i>and</i> the commit, so no other writer can observe
/// the window in which the database file and the rows disagree. It is correct for a single instance
/// only; a multi-instance deployment needs a Postgres advisory lock, which is deferred along with
/// everything else about running more than one of these.
/// </para>
/// </remarks>
internal interface IRepositoryDatabaseLock
{
    /// <summary>
    /// Waits for exclusive access to one repository's database.
    /// </summary>
    /// <param name="repositoryId">The repository to lock.</param>
    /// <param name="cancellationToken">A token to abandon the wait.</param>
    /// <returns>A handle that releases the lock when disposed.</returns>
    Task<IDisposable> AcquireAsync(Guid repositoryId, CancellationToken cancellationToken = default);
}

using System.Collections.Concurrent;

namespace PacmanManager.RepoHost.Infrastructure;

/// <summary>
/// The in-process <see cref="IRepositoryDatabaseLock"/>: one semaphore per repository id, held by
/// whichever request is currently rewriting that repository's database.
/// </summary>
/// <remarks>
/// Registered as a singleton, because a lock scoped to a request would lock nothing. The semaphores
/// are never evicted: one per repository this process has written to is the same order of magnitude
/// as the data itself, and a refcounted eviction is the classic place to introduce a race into the
/// very thing that exists to remove one.
/// </remarks>
internal sealed class RepositoryDatabaseLock : IRepositoryDatabaseLock
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    /// <inheritdoc />
    public async Task<IDisposable> AcquireAsync(Guid repositoryId, CancellationToken cancellationToken = default)
    {
        var semaphore = _locks.GetOrAdd(repositoryId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Handle(semaphore);
    }

    /// <summary>
    /// Releases the semaphore exactly once, however many times it is disposed.
    /// </summary>
    private sealed class Handle(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}

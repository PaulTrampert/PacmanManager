using PacmanManager.RepoHost.Infrastructure;

namespace PacmanManager.RepoHost.Test.Services.PackageServiceTests;

/// <summary>
/// An <see cref="IRepositoryDatabaseLock"/> that grants every request at once.
/// </summary>
/// <remarks>
/// The real lock is a Postgres advisory lock, which these in-memory fixtures have no database for.
/// They call the service one request at a time, so there is nothing for a lock to serialize;
/// <c>PostgresRepositoryDatabaseLockTests</c> is what shows the real one excludes.
/// </remarks>
internal sealed class NoOpRepositoryDatabaseLock : IRepositoryDatabaseLock
{
    /// <inheritdoc />
    public Task<IDisposable> AcquireAsync(Guid repositoryId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IDisposable>(new Handle());
    }

    private sealed class Handle : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

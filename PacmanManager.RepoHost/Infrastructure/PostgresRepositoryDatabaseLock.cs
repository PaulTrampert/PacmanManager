using System.Buffers.Binary;
using Npgsql;

namespace PacmanManager.RepoHost.Infrastructure;

/// <summary>
/// The <see cref="IRepositoryDatabaseLock"/> every process shares: a session-level Postgres advisory
/// lock, taken in the two-key form <c>pg_advisory_lock(key1, key2)</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>key1</c> is <see cref="RepositoryDatabasesKey"/>, naming the lock's purpose. Postgres keeps
/// one-key and two-key advisory locks apart, and Wolverine takes its own in the one-key space, so the
/// two never meet. <c>key2</c> is <see cref="DeriveRepositoryKey"/> of the repository's id. Two
/// repositories whose keys collide wait for each other, which costs waiting, never correctness.
/// </para>
/// <para>
/// Each <see cref="AcquireAsync"/> opens a connection of its own, never one a <c>DbContext</c> is
/// using, and holds it until the handle is disposed. Those connections are not pooled: a pooled
/// connection that was closed keeps its session, and with it the lock, until it is next reused. Not
/// pooling them means closing one ends its session, and a process that dies ends every session it
/// held, so Postgres releases its locks and a crashed process cannot leave a repository locked.
/// </para>
/// <para>
/// Registered as a singleton, since it owns the data source its connections come from.
/// </para>
/// </remarks>
internal sealed class PostgresRepositoryDatabaseLock : IRepositoryDatabaseLock, IAsyncDisposable, IDisposable
{
    /// <summary>
    /// The first key of every repository database lock, naming its purpose. Declared here and
    /// nowhere else.
    /// </summary>
    internal const int RepositoryDatabasesKey = 0x52_45_50_4F; // "REPO"

    private readonly NpgsqlDataSource _dataSource;

    /// <summary>
    /// Creates a lock that connects to the database <paramref name="connectionString"/> names.
    /// </summary>
    /// <param name="connectionString">
    /// A Npgsql connection string. Any pooling it asks for is turned off, as the remarks explain.
    /// </param>
    public PostgresRepositoryDatabaseLock(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };
        _dataSource = NpgsqlDataSource.Create(builder);
    }

    /// <summary>
    /// The second key of the lock on one repository: the XOR of its id's four 32-bit words.
    /// </summary>
    /// <remarks>
    /// Every bit of the id contributes, so version 7 ids created close together, which share their
    /// leading timestamp bytes, still get different keys. It is computed from the id's bytes alone,
    /// never <see cref="Guid.GetHashCode"/>, which .NET randomizes per process, because every process
    /// that takes the lock has to arrive at the same key.
    /// </remarks>
    /// <param name="repositoryId">The repository's id.</param>
    /// <returns>The key, the same for the same id in every process and runtime.</returns>
    internal static int DeriveRepositoryKey(Guid repositoryId)
    {
        Span<byte> bytes = stackalloc byte[16];
        repositoryId.TryWriteBytes(bytes);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes[..4])
               ^ BinaryPrimitives.ReadInt32LittleEndian(bytes[4..8])
               ^ BinaryPrimitives.ReadInt32LittleEndian(bytes[8..12])
               ^ BinaryPrimitives.ReadInt32LittleEndian(bytes[12..]);
    }

    /// <inheritdoc />
    public async Task<IDisposable> AcquireAsync(Guid repositoryId, CancellationToken cancellationToken = default)
    {
        var key = DeriveRepositoryKey(repositoryId);
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            // A cancelled token makes Npgsql cancel the statement on the server, which abandons the
            // wait. Should the lock be granted in the same instant, closing the connection below
            // ends the session and releases it, so a cancelled acquire never leaves the lock held.
            await using var command = new NpgsqlCommand("SELECT pg_advisory_lock($1, $2)", connection);
            command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = RepositoryDatabasesKey });
            command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = key });
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        return new Handle(connection, key);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    /// <inheritdoc />
    public void Dispose() => _dataSource.Dispose();

    /// <summary>
    /// Unlocks and closes the lock's connection exactly once, however many times it is disposed.
    /// </summary>
    private sealed class Handle(NpgsqlConnection connection, int key) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
            {
                return;
            }

            try
            {
                using var command = new NpgsqlCommand("SELECT pg_advisory_unlock($1, $2)", connection);
                command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = RepositoryDatabasesKey });
                command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = key });
                command.ExecuteNonQuery();
            }
            catch (NpgsqlException)
            {
                // The connection is already broken, and its session with it: Postgres has released
                // the lock. Closing the connection is all that is left to do.
            }
            finally
            {
                connection.Dispose();
            }
        }
    }
}

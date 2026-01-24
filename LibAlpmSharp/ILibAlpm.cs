using LibAlpmSharp.Interop;

namespace LibAlpmSharp;

public interface ILibAlpm : IDisposable
{
    /// <summary>
    /// Gets the root path for filesystem operations.
    /// </summary>
    string Root { get; }

    /// <summary>
    /// Gets the absolute path to the libalpm database.
    /// </summary>
    string DbPath { get; }

    /// <summary>
    /// Gets the current error code from the handle.
    /// </summary>
    /// <returns>The current error code.</returns>
    AlpmErrno GetLastError();

    /// <summary>
    /// Gets the local database containing installed packages.
    /// </summary>
    /// <returns>The local database.</returns>
    /// <exception cref="AlpmException">Thrown when the local database cannot be retrieved.</exception>
    IAlpmDatabase GetLocalDatabase();

    /// <summary>
    /// Gets the list of registered sync databases.
    /// </summary>
    /// <returns>A list of sync databases.</returns>
    IEnumerable<IAlpmDatabase> GetSyncDatabases();

    /// <summary>
    /// Registers a new sync database.
    /// </summary>
    /// <param name="name">The name of the sync repository (e.g., "core", "extra").</param>
    /// <param name="signatureLevel">The signature verification level for this database.</param>
    /// <returns>The newly registered database.</returns>
    /// <exception cref="ArgumentException">Thrown when name is null or empty.</exception>
    /// <exception cref="AlpmException">Thrown when the database cannot be registered.</exception>
    IAlpmDatabase RegisterSyncDatabase(string name, int signatureLevel = 0);
}
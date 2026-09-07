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
    /// Loads a package from a package file, such as one produced by <c>makepkg</c>.
    /// </summary>
    /// <remarks>
    /// The returned package owns its native handle and must be disposed. Because it comes from a
    /// file rather than a database, some of its metadata differs from a database package: it has
    /// no install date, and libalpm reports no checksums for it.
    /// </remarks>
    /// <param name="path">The path to the package file.</param>
    /// <param name="full">
    /// Whether to read the package's full file list. When false only the metadata in
    /// <c>.PKGINFO</c> is read, which is cheaper but leaves the file list empty.
    /// </param>
    /// <param name="sigLevel">The signature verification level to apply to the file.</param>
    /// <returns>The loaded package.</returns>
    /// <exception cref="ArgumentException">Thrown when path is null or empty.</exception>
    /// <exception cref="AlpmException">
    /// Thrown when the file is missing, is not a package, or fails signature verification.
    /// </exception>
    IPackage LoadPackageFile(string path, bool full = true, int sigLevel = 0);

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
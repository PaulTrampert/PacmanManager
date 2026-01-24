namespace LibAlpmSharp;

public interface IAlpmDatabase
{
    /// <summary>
    /// Gets the name of the database.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether this is the local database.
    /// </summary>
    bool IsLocal { get; }

    /// <summary>
    /// Returns a string that represents the current database.
    /// </summary>
    /// <returns>A string that represents the current database.</returns>
    string ToString();

    /// <summary>
    /// Gets the signature verification level for this database.
    /// </summary>
    /// <returns>The signature level flags.</returns>
    int GetSignatureLevel();

    /// <summary>
    /// Checks the validity of this database.
    /// </summary>
    /// <returns>True if the database is valid, false otherwise.</returns>
    /// <exception cref="AlpmException">Thrown when an error occurs.</exception>
    bool IsValid();

    /// <summary>
    /// Gets the list of servers configured for this database.
    /// </summary>
    /// <returns>A list of server URLs.</returns>
    IEnumerable<string> GetServers();

    /// <summary>
    /// Adds a server URL to this database.
    /// </summary>
    /// <param name="url">The server URL to add.</param>
    /// <exception cref="ArgumentException">Thrown when url is null or empty.</exception>
    /// <exception cref="AlpmException">Thrown when the operation fails.</exception>
    void AddServer(string url);

    /// <summary>
    /// Removes a server URL from this database.
    /// </summary>
    /// <param name="url">The server URL to remove.</param>
    /// <returns>True if the server was removed, false if it was not found.</returns>
    /// <exception cref="ArgumentException">Thrown when url is null or empty.</exception>
    /// <exception cref="AlpmException">Thrown when an error occurs.</exception>
    bool RemoveServer(string url);

    /// <summary>
    /// Gets a package from this database by name.
    /// </summary>
    /// <param name="name">The name of the package to find.</param>
    /// <returns>The package if found, null otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when name is null or empty.</exception>
    IPackage? GetPackage(string name);

    /// <summary>
    /// Gets all packages in this database.
    /// </summary>
    /// <returns>A list of all packages in the database.</returns>
    IEnumerable<IPackage> GetPackages();

    /// <summary>
    /// Searches for packages in this database matching the given terms.
    /// </summary>
    /// <param name="searchTerms">The search terms to match against package names and descriptions.</param>
    /// <returns>A list of packages matching the search terms.</returns>
    /// <exception cref="ArgumentException">Thrown when searchTerms is null or empty.</exception>
    /// <exception cref="AlpmException">Thrown when the search fails.</exception>
    IEnumerable<IPackage> Search(params string[] searchTerms);
}
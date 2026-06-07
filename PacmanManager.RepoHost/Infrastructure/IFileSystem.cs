namespace PacmanManager.RepoHost.Infrastructure;

/// <summary>
/// Defines the operations for interacting with the file system.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Determines whether the specified file exists.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>True if the file exists; otherwise, false.</returns>
    bool Exists(string path);

    /// <summary>
    /// Deletes the specified file.
    /// </summary>
    /// <param name="path">The path to the file to be deleted.</param>
    void Delete(string path);

    /// <summary>
    /// Opens a file for reading.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>A <see cref="Stream"/> object to read from the file.</returns>
    Stream OpenRead(string path);
}

namespace PacmanManager.RepoHost.Infrastructure;

/// <summary>
/// Defines the operations for interacting with the file system.
/// </summary>
/// <remarks>
/// Everything that touches the disk goes through this interface, so that the services which write,
/// move and remove package files stay unit testable without a real file system underneath them.
/// </remarks>
public interface IFileSystem
{
    /// <summary>
    /// Determines whether the specified file exists.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>True if the file exists; otherwise, false.</returns>
    bool Exists(string path);

    /// <summary>
    /// Determines whether the specified directory exists.
    /// </summary>
    /// <param name="path">The path to the directory.</param>
    /// <returns>True if the directory exists; otherwise, false.</returns>
    bool DirectoryExists(string path);

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

    /// <summary>
    /// Creates a file for writing, truncating it if it already exists.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>A writable <see cref="Stream"/> positioned at the start of the file.</returns>
    Stream OpenWrite(string path);

    /// <summary>
    /// Moves a file to a new location.
    /// </summary>
    /// <param name="sourcePath">The file to move.</param>
    /// <param name="destinationPath">The path to move it to.</param>
    /// <param name="overwrite">Whether an existing file at <paramref name="destinationPath"/> may be replaced.</param>
    void Move(string sourcePath, string destinationPath, bool overwrite = false);

    /// <summary>
    /// Creates a directory and any missing parents. Succeeds when the directory already exists.
    /// </summary>
    /// <param name="path">The directory to create.</param>
    void CreateDirectory(string path);
}

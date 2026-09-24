namespace PacmanManager.RepoHost.Infrastructure;

/// <summary>
/// A physical implementation of the <see cref="IFileSystem"/> interface that uses the standard .NET <see cref="File"/> class.
/// </summary>
public class PhysicalFileSystem : IFileSystem
{
    /// <inheritdoc/>
    public bool Exists(string path) => File.Exists(path);

    /// <inheritdoc/>
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc/>
    public void Delete(string path) => File.Delete(path);

    /// <inheritdoc/>
    public Stream OpenRead(string path) => File.OpenRead(path);

    /// <inheritdoc/>
    public DateTimeOffset GetLastWriteTimeUtc(string path)
    {
        // File.GetLastWriteTimeUtc reports 1601-01-01 for a file that does not exist rather than
        // throwing, which would turn a missing file into a very old one.
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"'{path}' does not exist.", path);
        }

        return new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
    }

    /// <inheritdoc/>
    public Stream OpenWrite(string path) => File.Create(path);

    /// <inheritdoc/>
    public void Move(string sourcePath, string destinationPath, bool overwrite = false) =>
        File.Move(sourcePath, destinationPath, overwrite);

    /// <inheritdoc/>
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    /// <inheritdoc/>
    public void DeleteDirectory(string path) => Directory.Delete(path, recursive: true);
}

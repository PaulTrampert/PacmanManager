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
    public Stream OpenWrite(string path) => File.Create(path);

    /// <inheritdoc/>
    public void Move(string sourcePath, string destinationPath, bool overwrite = false) =>
        File.Move(sourcePath, destinationPath, overwrite);

    /// <inheritdoc/>
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
}

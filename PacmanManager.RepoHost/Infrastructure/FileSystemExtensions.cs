using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Infrastructure;

/// <summary>
/// Reading a file a repository serves through <see cref="IFileSystem"/>.
/// </summary>
internal static class FileSystemExtensions
{
    /// <summary>
    /// Opens a file together with its modification time, or reports it absent.
    /// </summary>
    /// <param name="fileSystem">The file system to read from.</param>
    /// <param name="path">The file to open.</param>
    /// <returns>The open file, or null when there is no file at <paramref name="path"/>.</returns>
    /// <remarks>
    /// <para>
    /// A file that is not there is absent rather than an error, including when it disappears between
    /// the existence check and the open: <c>repo-add</c> rotates a database by rename, so a reader can
    /// briefly see it missing, and the client's next attempt succeeds.
    /// </para>
    /// <para>
    /// The time is read before the file is opened. If the file is replaced in between, the stream then
    /// holds newer bytes than the time says, so the client merely fetches it again next time. Reading
    /// it afterwards could pair older bytes with a newer time, which a client would treat as current
    /// until the file next changed.
    /// </para>
    /// </remarks>
    public static RepositoryFile? OpenRepositoryFile(this IFileSystem fileSystem, string path)
    {
        if (!fileSystem.Exists(path))
        {
            return null;
        }

        try
        {
            var lastModified = fileSystem.GetLastWriteTimeUtc(path);
            return new RepositoryFile(fileSystem.OpenRead(path), lastModified);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }
}

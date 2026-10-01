using PacmanManager.Entities;
using PacmanManager.RepoHost.CliTools;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// The default <see cref="IPacmanRepoService"/>, composing <see cref="IRepositoryService"/> with the
/// package file layout <see cref="IPackagePathResolver"/> owns.
/// </summary>
/// <remarks>
/// This class deliberately takes no database context. Visibility is decided by
/// <see cref="IRepositoryService"/> alone, so there is exactly one statement of the repository
/// visibility rule to keep correct; <c>PacmanRepoServiceEnforcementTests</c> holds it to that.
/// </remarks>
internal class PacmanRepoService(
    IRepositoryService repositories,
    IPackagePathResolver pathResolver,
    IFileSystem fileSystem) : IPacmanRepoService
{
    /// <summary>
    /// The extensions a database is requested by, <c>.db</c> and <c>.files</c> being the names pacman
    /// uses, and the kind of database each names.
    /// </summary>
    private static readonly Dictionary<string, RepositoryDatabaseKind> DatabaseExtensions = new(StringComparer.Ordinal)
    {
        [".db"] = RepositoryDatabaseKind.Sync,
        [RepositoryDatabase.FileExtension] = RepositoryDatabaseKind.Sync,
        [".files"] = RepositoryDatabaseKind.Files,
        [RepositoryDatabase.FilesFileExtension] = RepositoryDatabaseKind.Files,
    };

    /// <inheritdoc/>
    public async Task<RepositoryFile?> ResolveAsync(
        string repoName,
        string repoArch,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var repository = await repositories.GetRepositoryByNameAsync(repoName, cancellationToken);
        if (repository is null || !repository.SupportedArchitectures.Contains(repoArch, StringComparer.Ordinal))
        {
            return null;
        }

        // A database is the repository's own name plus one of the database extensions; a database
        // requested under any other name is not this repository's.
        if (fileName.StartsWith(repository.Name, StringComparison.Ordinal)
            && DatabaseExtensions.TryGetValue(fileName[repository.Name.Length..], out var kind))
        {
            return await repositories.GetRepositoryDatabaseByIdAsync(
                repository.Id, repoArch, kind, cancellationToken);
        }

        return OpenPackageFile(repository.Id, fileName);
    }

    /// <summary>
    /// Opens a package file straight from the repository's directory. No package row is consulted,
    /// and the name is not parsed: the directory holds only packages that were validated on upload,
    /// and a client only asks for a basename it read out of the repository's database.
    /// </summary>
    private RepositoryFile? OpenPackageFile(Guid repositoryId, string fileName)
    {
        string path;
        try
        {
            path = pathResolver.GetPackageFilePath(repositoryId, fileName);
        }
        catch (ArgumentException)
        {
            // Not a plain basename (a separator, a control character, '.' or '..'). The route keeps
            // a '/' out of the segment already; this keeps the rest from reaching the disk.
            return null;
        }

        return fileSystem.OpenRepositoryFile(path);
    }
}

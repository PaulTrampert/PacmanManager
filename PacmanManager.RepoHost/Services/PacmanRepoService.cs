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
    /// The extensions a sync database is requested by, <c>.db</c> being the name pacman uses.
    /// </summary>
    private static readonly string[] SyncDatabaseExtensions = [".db", RepositoryDatabase.FileExtension];

    /// <summary>
    /// The extensions a files database is requested by, <c>.files</c> being the name pacman uses.
    /// </summary>
    private static readonly string[] FilesDatabaseExtensions = [".files", RepositoryDatabase.FilesFileExtension];

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

        if (IsDatabaseName(fileName, repository.Name, SyncDatabaseExtensions))
        {
            return await repositories.GetRepositoryDatabaseByIdAsync(
                repository.Id, repoArch, RepositoryDatabaseKind.Sync, cancellationToken);
        }

        if (IsDatabaseName(fileName, repository.Name, FilesDatabaseExtensions))
        {
            return await repositories.GetRepositoryDatabaseByIdAsync(
                repository.Id, repoArch, RepositoryDatabaseKind.Files, cancellationToken);
        }

        return OpenPackageFile(repository.Id, repoArch, fileName);
    }

    /// <summary>
    /// Whether <paramref name="fileName"/> names the repository's database under one of
    /// <paramref name="extensions"/>. A database requested under any other repository's name is not
    /// this repository's.
    /// </summary>
    private static bool IsDatabaseName(string fileName, string repositoryName, IEnumerable<string> extensions) =>
        extensions.Any(extension => string.Equals(fileName, repositoryName + extension, StringComparison.Ordinal));

    /// <summary>
    /// Opens a package file straight from the repository's directory, once its name has been
    /// validated. No package row is consulted: a client only asks for a basename it read out of the
    /// repository's database, and <c>repo-add</c> built that database from these very files.
    /// </summary>
    private RepositoryFile? OpenPackageFile(Guid repositoryId, string repoArch, string fileName)
    {
        // Validation runs before any file system call, and admits nothing but a plain basename of a
        // package file's shape: no separator, no signature, no database, no subdirectory.
        if (!pathResolver.TryParseFileName(fileName, out var package))
        {
            return null;
        }

        // An any package is stored once and served under every architecture's URL.
        if (!string.Equals(package.Architecture, repoArch, StringComparison.Ordinal)
            && !string.Equals(package.Architecture, Architectures.Any, StringComparison.Ordinal))
        {
            return null;
        }

        return fileSystem.OpenRepositoryFile(pathResolver.GetPackageFilePath(repositoryId, fileName));
    }
}

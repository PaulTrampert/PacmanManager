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
    public async Task<PacmanResolution?> ResolveAsync(
        string repoName,
        string repoArch,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var repository = await repositories.GetRepositoryByNameAsync(repoName, cancellationToken);
        if (repository is null)
        {
            // Live names and retired names are one namespace, so a name no live repository holds is
            // the only one worth looking for among the retired.
            return await ResolveRetiredAsync(repoName, repoArch, fileName, cancellationToken);
        }

        if (!Supports(repository, repoArch))
        {
            return null;
        }

        var file = IsDatabaseName(repository.Name, fileName, out var kind)
            ? await repositories.GetRepositoryDatabaseByIdAsync(repository.Id, repoArch, kind, cancellationToken)
            : OpenPackageFile(repository.Id, fileName);

        return file is null ? null : PacmanResolution.Serve(file);
    }

    /// <summary>
    /// Resolves a path naming a repository by a name it was renamed away from: a redirect to the same
    /// file under the repository's current name while the redirect window lasts, and gone after it.
    /// </summary>
    /// <remarks>
    /// The redirect is to the name the repository has now, so a repository renamed twice is reached
    /// from either old name in one hop. Nothing about the file is checked here; the target answers
    /// for itself.
    /// </remarks>
    private async Task<PacmanResolution?> ResolveRetiredAsync(
        string repoName,
        string repoArch,
        string fileName,
        CancellationToken cancellationToken)
    {
        var retired = await repositories.GetRetiredRepositoryNameAsync(repoName, cancellationToken);
        if (retired is null || !Supports(retired.Repository, repoArch))
        {
            return null;
        }

        if (!retired.IsRedirecting)
        {
            return PacmanResolution.Gone;
        }

        var currentName = retired.Repository.Name;
        var targetFileName = IsDatabaseName(repoName, fileName, out _)
            ? currentName + fileName[repoName.Length..]
            : fileName;

        return PacmanResolution.RedirectTo(new PacmanRedirect(currentName, targetFileName));
    }

    /// <summary>
    /// Whether a repository publishes a database for <paramref name="repoArch"/>, matched exactly.
    /// </summary>
    private static bool Supports(Repository repository, string repoArch) =>
        repository.SupportedArchitectures.Contains(repoArch, StringComparer.Ordinal);

    /// <summary>
    /// Whether <paramref name="fileName"/> names one of the databases of a repository called
    /// <paramref name="repoName"/>: the name itself plus one of the database extensions. A database
    /// requested under any other name is not that repository's.
    /// </summary>
    private static bool IsDatabaseName(string repoName, string fileName, out RepositoryDatabaseKind kind)
    {
        kind = default;
        return fileName.StartsWith(repoName, StringComparison.Ordinal)
               && DatabaseExtensions.TryGetValue(fileName[repoName.Length..], out kind);
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

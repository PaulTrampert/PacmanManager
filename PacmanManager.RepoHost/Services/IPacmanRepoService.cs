using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Resolves a path under the <c>/pacman/{repoName}/{repoArch}/{fileName}</c> route root to the file a
/// <c>pacman</c> client is asking for.
/// </summary>
/// <remarks>
/// <para>
/// Resolution owns no database access and no authorization rule of its own. The repository is looked
/// up through <see cref="IRepositoryService.GetRepositoryByNameAsync"/>, which already hides a
/// repository the current actor may not see, and its databases are read through
/// <see cref="IRepositoryService"/> too. The one check made here is whether the repository supports
/// the requested architecture.
/// </para>
/// <para>
/// The repository name and architecture are matched exactly as stored. The file name is one of
/// <c>{repoName}.db</c>, <c>{repoName}.db.tar.gz</c>, <c>{repoName}.files</c>,
/// <c>{repoName}.files.tar.gz</c>, or a package file name of the form
/// <c>{name}-{version}-{architecture}.pkg.tar.{ext}</c> whose architecture is <c>{repoArch}</c> or
/// <c>any</c>, which is read straight from the repository's directory. Anything else resolves to
/// nothing without the disk being touched.
/// </para>
/// </remarks>
public interface IPacmanRepoService
{
    /// <summary>
    /// Resolves a requested file.
    /// </summary>
    /// <param name="repoName">The repository's name, pacman's <c>$repo</c>.</param>
    /// <param name="repoArch">The architecture, pacman's <c>$arch</c>.</param>
    /// <param name="fileName">The requested file name, a single path segment.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The open file and its modification time, or null when the repository is absent as far as the
    /// current actor is concerned, does not support <paramref name="repoArch"/>, the file name is not
    /// one the repository serves, or the file is not on disk.
    /// </returns>
    Task<RepositoryFile?> ResolveAsync(
        string repoName,
        string repoArch,
        string fileName,
        CancellationToken cancellationToken = default);
}

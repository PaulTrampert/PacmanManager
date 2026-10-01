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
/// <c>{repoName}.files.tar.gz</c>, or the name of a package file, which is read straight from the
/// repository's directory without being parsed. A name that is not a plain basename resolves to
/// nothing without the disk being touched.
/// </para>
/// <para>
/// A repository name that no live repository holds may be one a repository was renamed away from,
/// which is looked up through <see cref="IRepositoryService.GetRetiredRepositoryNameAsync"/>, under
/// the same visibility rule as a live name.
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
    /// <para>
    /// The open file and its modification time, when <paramref name="repoName"/> is a live
    /// repository's name.
    /// </para>
    /// <para>
    /// When it is a name a repository was renamed away from: a redirect to the same file under the
    /// repository's current name while the redirect window lasts, and <see cref="PacmanResolution.Gone"/>
    /// after it. A database requested by the old name is redirected to the database named for the new
    /// one.
    /// </para>
    /// <para>
    /// <see cref="PacmanResolution.RepositoryNotFound"/> when the repository is absent as far as the
    /// current actor is concerned, whether it does not exist or is private to somebody else; or
    /// <see cref="PacmanResolution.FileNotFound"/> when the repository is visible but does not support
    /// <paramref name="repoArch"/>, the file name is not one it serves, or the file is not on disk.
    /// </para>
    /// </returns>
    Task<PacmanResolution> ResolveAsync(
        string repoName,
        string repoArch,
        string fileName,
        CancellationToken cancellationToken = default);
}

using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.Packages.Exceptions;

namespace PacmanManager.Packages;

/// <summary>
/// Publishes packages into a repository and removes them from it: the file in the repository
/// tree, the entries <c>repo-add</c> and <c>repo-remove</c> keep in its databases, and the package
/// rows, kept in step with one another.
/// </summary>
/// <remarks>
/// <para>
/// <b>No permission is checked here.</b> The caller decides whether the change may be made, against
/// whatever actor it runs as, and hands over a repository it has already authorized: RepoHost's
/// <c>PackageService</c> asks <see cref="Authorization.PackageAccessPolicy"/> first, and a
/// background job is authorized when it is scheduled.
/// </para>
/// <para>
/// Each change takes the repository's <see cref="Infrastructure.IRepositoryDatabaseLock"/> and holds
/// it across the side effects and the commit, so it is serialized with every other change to the
/// same repository, in any process.
/// </para>
/// </remarks>
public interface IPackagePublisher
{
    /// <summary>
    /// Publishes <paramref name="upload"/> into <paramref name="repository"/>, replacing whichever
    /// builds of the same name it supersedes.
    /// </summary>
    /// <param name="repository">
    /// The repository published into, tracked by the same <see cref="PacmanManagerDbContext"/> this
    /// publisher commits with, since its <c>UpdatedAt</c> moves with its contents.
    /// </param>
    /// <param name="publisher">The user recorded as the package's publisher.</param>
    /// <param name="upload">
    /// The received file. A publish that commits moves it into the repository tree; one that fails
    /// leaves the caller to discard it.
    /// </param>
    /// <param name="cancellationToken">Cancels the publish. A cancelled publish still undoes what it did.</param>
    /// <returns>The package as committed, and whether it is new to the repository.</returns>
    /// <exception cref="UnreadablePackageException">libalpm could not read the file as a package.</exception>
    /// <exception cref="PackageArchitectureMismatchException">The repository does not serve the package's architecture.</exception>
    /// <exception cref="UnsupportedPackageCompressionException">The file is not in an allowed compression format.</exception>
    /// <exception cref="InvalidPackageMetadataException">The package's metadata could not name a file.</exception>
    /// <exception cref="PackageNotNewerException">A build it would supersede is the same version or newer.</exception>
    /// <exception cref="CliToolFailedException"><c>repo-add</c> or <c>repo-remove</c> failed, and nothing was published.</exception>
    Task<PublishedPackage> PublishAsync(
        PacmanRepository repository,
        User publisher,
        PackageUpload upload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes <paramref name="package"/> from <paramref name="repository"/>: its entry from every
    /// database that lists it, its row, and then its file.
    /// </summary>
    /// <param name="repository">
    /// The package's repository, tracked by the same <see cref="PacmanManagerDbContext"/> this
    /// publisher commits with.
    /// </param>
    /// <param name="package">The package to remove, tracked by the same context.</param>
    /// <param name="cancellationToken">Cancels the removal. A cancelled removal still undoes what it did.</param>
    /// <returns>A task that completes once the removal has committed.</returns>
    /// <exception cref="CliToolFailedException"><c>repo-remove</c> failed, and nothing was removed.</exception>
    Task RemoveAsync(PacmanRepository repository, PacmanPackage package, CancellationToken cancellationToken = default);
}

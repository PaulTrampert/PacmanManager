using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Provides services for reading the packages published into hosted repositories.
/// </summary>
/// <remarks>
/// <para>
/// Packages have no visibility of their own: a package is visible exactly when its repository is.
/// Every method on this interface enforces that against the actor supplied by
/// <see cref="IActorAccessor"/> for the current scope, so an implementation is safe to call from a
/// controller, a CLI tool or a background job without the caller reasoning about authorization at
/// all.
/// </para>
/// <para>
/// Packages the actor is not entitled to know about are reported as missing rather than forbidden,
/// so a <c>null</c> result means "absent, as far as you are concerned" and covers three cases that
/// have to be indistinguishable from outside: the package does not exist, the repository does not
/// exist, and the repository is private and belongs to someone else.
/// </para>
/// </remarks>
public interface IPackageService
{
    /// <summary>
    /// Retrieves a paginated list of the packages visible to the current actor.
    /// </summary>
    /// <param name="paginationParams">The pagination parameters.</param>
    /// <param name="filter">Caller-supplied criteria, which can only narrow the visible set.</param>
    /// <param name="sort">
    /// The order to return results in. Defaults to package name; a caller who names a sort field but
    /// no direction gets that field's own default, which is A→Z for the name and biggest or newest
    /// first for the rest.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A paginated response containing packages.</returns>
    Task<PaginatedResponse<Package>> GetPackagesAsync(
        PaginationParams paginationParams,
        PackageFilter? filter = null,
        SortOptions<PackageSortField>? sort = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a package by its ID.
    /// </summary>
    /// <param name="id">The ID of the package.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The package if it exists and is visible to the current actor; otherwise, null.</returns>
    Task<Package?> GetPackageByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a package by its natural key.
    /// </summary>
    /// <param name="repositoryId">The ID of the repository holding the package.</param>
    /// <param name="name">The package name.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The package if it exists and is visible to the current actor; otherwise, null.</returns>
    /// <remarks>
    /// A repository holds exactly one version of a package, so <c>(repositoryId, name)</c> matches
    /// at most one row. It is the key a build script actually knows, having just built a package for
    /// a repository without ever seeing a package id.
    /// </remarks>
    Task<Package?> GetPackageByNameAsync(Guid repositoryId, string name, CancellationToken cancellationToken = default);
}

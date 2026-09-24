using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Provides services for managing and interacting with repositories.
/// </summary>
/// <remarks>
/// <para>
/// Every method on this interface enforces the repository authorization rules against the actor
/// supplied by <see cref="IActorAccessor"/> for the current scope. No method takes a user,
/// principal or permission argument, and no caller can opt out of the checks: an implementation
/// is safe to call from a controller, a CLI tool or a background job without the caller
/// reasoning about authorization at all.
/// </para>
/// <para>
/// Repositories the actor is not entitled to know about are reported as missing rather than
/// forbidden, so a <c>null</c> or <c>false</c> result means "absent, as far as you are concerned"
/// and not necessarily "absent".
/// </para>
/// </remarks>
public interface IRepositoryService
{
    /// <summary>
    /// Retrieves a repository by its ID.
    /// </summary>
    /// <param name="id">The ID of the repository.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The repository if it exists and is visible to the current actor; otherwise, null.</returns>
    Task<Repository?> GetRepositoryByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a repository by its name.
    /// </summary>
    /// <param name="name">The repository's name, matched exactly as stored.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The repository if it exists and is visible to the current actor; otherwise, null.</returns>
    /// <remarks>
    /// Repository names are unique across the whole deployment, so a name alone identifies at most
    /// one repository. A repository the current actor may not see is reported as absent.
    /// </remarks>
    Task<Repository?> GetRepositoryByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the database file stream for one of a repository's architectures by its ID.
    /// </summary>
    /// <param name="id">The ID of the repository.</param>
    /// <param name="architecture">
    /// The architecture whose database to read. A repository publishes one database per supported
    /// architecture.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A stream to the architecture's database file if the repository is visible to the current actor
    /// and supports <paramref name="architecture"/>; otherwise, null.
    /// </returns>
    Task<Stream?> GetRepositoryFileByIdAsync(Guid id, string architecture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the database file stream for one of a repository's architectures by its name.
    /// </summary>
    /// <param name="name">The repository's name, matched exactly as stored.</param>
    /// <param name="architecture">
    /// The architecture whose database to read. A repository publishes one database per supported
    /// architecture.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A stream to the architecture's database file if the repository is visible to the current actor
    /// and supports <paramref name="architecture"/>; otherwise, null.
    /// </returns>
    Task<Stream?> GetRepositoryFileByNameAsync(string name, string architecture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new repository owned by the current actor's user.
    /// </summary>
    /// <param name="request">The request object containing repository details.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The newly created repository.</returns>
    /// <exception cref="NoCurrentUserException">Thrown when there is no user to own the repository.</exception>
    /// <exception cref="ItemExistsException">Thrown when the user already owns a repository with the same name and architecture.</exception>
    Task<Repository> CreateRepositoryAsync(WriteRepositoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing repository.
    /// </summary>
    /// <param name="id">The ID of the repository to update.</param>
    /// <param name="update">The update request containing new details.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The updated repository, or null if it does not exist or is not visible to the current actor.</returns>
    /// <exception cref="NoCurrentUserException">Thrown when the operation requires an identity and there is none.</exception>
    /// <exception cref="RepositoryForbiddenException">Thrown when the actor may see the repository but does not own it.</exception>
    /// <exception cref="ItemExistsException">Thrown when the owner already has another repository with the new name and architecture.</exception>
    Task<Repository?> UpdateRepositoryAsync(Guid id, WriteRepositoryRequest update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a repository and its backing database file.
    /// </summary>
    /// <param name="id">The ID of the repository to delete.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the repository was deleted; false if it does not exist or is not visible to the current actor.</returns>
    /// <exception cref="NoCurrentUserException">Thrown when the operation requires an identity and there is none.</exception>
    /// <exception cref="RepositoryForbiddenException">Thrown when the actor may see the repository but does not own it.</exception>
    Task<bool> DeleteRepositoryAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of the repositories visible to the current actor.
    /// </summary>
    /// <param name="paginationParams">The pagination parameters.</param>
    /// <param name="filter">Caller-supplied criteria, which can only narrow the visible set.</param>
    /// <param name="sort">
    /// The order to return results in. Defaults to newest first; a caller who names a sort field
    /// but no direction gets that field's own default, which is A→Z for the name.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A paginated response containing repositories.</returns>
    Task<PaginatedResponse<Repository>> GetRepositoriesAsync(
        PaginationParams paginationParams,
        RepositoryFilter? filter = null,
        SortOptions<RepositorySortField>? sort = null,
        CancellationToken cancellationToken = default);
}

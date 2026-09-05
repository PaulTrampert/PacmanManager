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
    /// <param name="name">The name of the repository.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The repository if it exists and is visible to the current actor; otherwise, null.</returns>
    Task<Repository?> GetRepositoryByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the file stream for a repository by its ID.
    /// </summary>
    /// <param name="id">The ID of the repository.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A stream to the repository file if it is visible to the current actor; otherwise, null.</returns>
    Task<Stream?> GetRepositoryFileByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the file stream for a repository by its name.
    /// </summary>
    /// <param name="name">The name of the repository.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A stream to the repository file if it is visible to the current actor; otherwise, null.</returns>
    Task<Stream?> GetRepositoryFileByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new repository owned by the current actor's user.
    /// </summary>
    /// <param name="request">The request object containing repository details.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The newly created repository.</returns>
    /// <exception cref="NoCurrentUserException">Thrown when there is no user to own the repository.</exception>
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
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A paginated response containing repositories.</returns>
    Task<PaginatedResponse<Repository>> GetRepositoriesAsync(
        PaginationParams paginationParams,
        RepositoryFilter? filter = null,
        CancellationToken cancellationToken = default);
}

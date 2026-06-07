using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Provides services for managing and interacting with repositories.
/// </summary>
public interface IRepositoryService
{
    /// <summary>
    /// Retrieves a repository by its ID.
    /// </summary>
    /// <param name="id">The ID of the repository.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The repository if found; otherwise, null.</returns>
    Task<Repository?> GetRepositoryByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a repository by its name.
    /// </summary>
    /// <param name="name">The name of the repository.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The repository if found; otherwise, null.</returns>
    Task<Repository?> GetRepositoryByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the file stream for a repository by its ID.
    /// </summary>
    /// <param name="id">The ID of the repository.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A stream to the repository file if found; otherwise, null.</returns>
    Task<Stream?> GetRepositoryFileByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the file stream for a repository by its name.
    /// </summary>
    /// <param name="name">The name of the repository.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A stream to the repository file by its name.</returns>
    Task<Stream?> GetRepositoryFileByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new repository.
    /// </summary>
    /// <param name="request">The request object containing repository details.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The newly created repository.</returns>
    Task<Repository> CreateRepositoryAsync(WriteRepositoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of repositories.
    /// </summary>
    /// <param name="paginationParams">The pagination parameters.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A paginated response containing repositories.</returns>
    Task<PaginatedResponse<Repository>> GetRepositoriesAsync(PaginationParams paginationParams, CancellationToken cancellationToken = default);
}

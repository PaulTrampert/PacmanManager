using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Serves the users API: reading users, and the caller's own user record.
/// </summary>
/// <remarks>
/// Separate from <see cref="IUserService"/>, which is used before authentication has established a
/// current user. This service deals only in wire models, never the <c>User</c> entity, so nothing it
/// returns can carry an email to a response that should not show one.
/// </remarks>
public interface IUserManagementService
{
    /// <summary>
    /// Gets the current user's own record.
    /// </summary>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>The current user, projected to <see cref="CurrentUser"/>.</returns>
    /// <exception cref="NoCurrentUserException">Thrown if there is no current user.</exception>
    Task<CurrentUser> GetCurrentUserAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a user by id. Every user is readable by everyone, so there is no access check.
    /// </summary>
    /// <param name="userId">The id of the user to look up.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>The user projected to <see cref="PublicUserInfo"/>, or <c>null</c> if no user has that id.</returns>
    Task<PublicUserInfo?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a page of users. Every user is readable by everyone, so there is no access check,
    /// and the listing may be called anonymously.
    /// </summary>
    /// <param name="paginationParams">The pagination parameters.</param>
    /// <param name="filter">Caller-supplied criteria for narrowing the listing.</param>
    /// <param name="sort">The order to return results in. Defaults to alphabetical by display name.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A page of users, projected to <see cref="PublicUserInfo"/> so that no email is included.</returns>
    Task<PaginatedResponse<PublicUserInfo>> ListUsersAsync(
        PaginationParams paginationParams,
        UserFilter? filter = null,
        SortOptions<UserSortField>? sort = null,
        CancellationToken ct = default);
}

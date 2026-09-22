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
}

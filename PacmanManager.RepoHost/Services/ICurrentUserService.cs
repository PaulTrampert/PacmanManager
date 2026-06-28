using PacmanManager.Entities;
using PacmanManager.RepoHost.Exceptions;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Service for accessing the current user.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Get the current user, or null if there is none.
    /// </summary>
    /// <returns>The current user, or null if there is none.</returns>
    Task<User?> GetCurrentUserAsync();
    
    /// <summary>
    /// Get the current user, or throw if there is none.
    /// </summary>
    /// <returns>The current user.</returns>
    /// <exception cref="NoCurrentUserException">Thrown if there is no current user.</exception>
    Task<User> RequireCurrentUserAsync();
}
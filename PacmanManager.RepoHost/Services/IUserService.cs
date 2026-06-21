using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Provides services for managing users and their identity links.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Retrieves a user by their external identity provider identifiers.
    /// </summary>
    /// <param name="authority">The identity provider authority.</param>
    /// <param name="subject">The subject identifier from the identity provider.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="User"/> if found; otherwise, <c>null</c>.</returns>
    Task<User?> GetUserByExternalIdAsync(string authority, string subject, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a user by their email address.
    /// </summary>
    /// <param name="email">The email address of the user.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="User"/> if found; otherwise, <c>null</c>.</returns>
    Task<User?> GetUserByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Creates a new user in the system.
    /// </summary>
    /// <param name="user">The user entity to create.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the created <see cref="User"/>.</returns>
    Task<User> CreateUserAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Links an existing user to an external identity provider.
    /// </summary>
    /// <param name="user">The user to link.</param>
    /// <param name="authority">The identity provider authority.</param>
    /// <param name="subject">The subject identifier from the identity provider.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the updated <see cref="User"/>.</returns>
    Task<User> LinkToIdentityAsync(
        User user, 
        string authority, 
        string subject,
        CancellationToken ct = default);

    /// <summary>
    /// Ensures that a user exists and is linked to the provided identity information. 
    /// If the user does not exist, one will be created.
    /// </summary>
    /// <param name="email">The email address of the user.</param>
    /// <param name="displayName">The display name for the user.</param>
    /// <param name="authority">The identity provider authority.</param>
    /// <param name="subject">The subject identifier from the identity provider.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="User"/>.</returns>
    Task<User> EnsureUserLinkedAsync(string email, string displayName, string authority, string subject, CancellationToken ct = default);
}
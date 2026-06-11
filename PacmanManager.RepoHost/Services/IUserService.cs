using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Services;

public interface IUserService
{
    Task<User?> GetUserByExternalIdAsync(string authority, string subject, CancellationToken ct = default);
    Task<User?> GetUserByEmailAsync(string email, CancellationToken ct = default);
    Task<User> CreateUserAsync(User user, CancellationToken ct = default);

    Task<User> LinkToIdentityAsync(
        User user, 
        string authority, 
        string subject,
        CancellationToken ct = default);
}
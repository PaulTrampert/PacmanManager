using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Implementation of <see cref="IUserManagementService"/>.
/// </summary>
/// <param name="currentUserService">Resolves the caller for the methods that act on the current user.</param>
public class UserManagementService(ICurrentUserService currentUserService) : IUserManagementService
{
    /// <inheritdoc />
    public async Task<CurrentUser> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var user = await currentUserService.GetCurrentUserAsync(ct)
                   ?? throw new NoCurrentUserException();

        return CurrentUser.FromUser(user);
    }
}

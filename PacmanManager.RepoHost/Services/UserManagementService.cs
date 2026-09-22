using Microsoft.EntityFrameworkCore;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Implementation of <see cref="IUserManagementService"/>.
/// </summary>
/// <param name="currentUserService">Resolves the caller for the methods that act on the current user.</param>
/// <param name="dbContext">The database the users are read from.</param>
public class UserManagementService(
    ICurrentUserService currentUserService,
    PacmanManagerDbContext dbContext) : IUserManagementService
{
    /// <inheritdoc />
    public async Task<CurrentUser> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var user = await currentUserService.GetCurrentUserAsync(ct)
                   ?? throw new NoCurrentUserException();

        return CurrentUser.FromUser(user);
    }

    /// <inheritdoc />
    public async Task<PublicUserInfo?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new PublicUserInfo
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
            })
            .SingleOrDefaultAsync(ct);
    }
}

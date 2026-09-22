using Microsoft.EntityFrameworkCore;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Implementation of <see cref="IUserManagementService"/>.
/// </summary>
/// <param name="actorAccessor">Supplies the caller for the methods that act on the current user.</param>
/// <param name="dbContext">The database the users are read from.</param>
public class UserManagementService(
    IActorAccessor actorAccessor,
    PacmanManagerDbContext dbContext) : IUserManagementService
{
    /// <inheritdoc />
    public async Task<CurrentUser> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var actor = await actorAccessor.GetActorAsync(ct);
        var user = actor.User ?? throw new NoCurrentUserException();

        return CurrentUser.FromUser(user);
    }

    /// <inheritdoc />
    public async Task<PublicUserInfo?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => new PublicUserInfo
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
            })
            .SingleOrDefaultAsync(ct);
    }
}

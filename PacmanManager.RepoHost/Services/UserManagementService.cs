using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Models;
using PTrampert.QueryObjects;

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
            .Select(PublicUserInfoProjection)
            .SingleOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<PaginatedResponse<PublicUserInfo>> ListUsersAsync(
        PaginationParams paginationParams,
        UserFilter? filter = null,
        SortOptions<UserSortField>? sort = null,
        CancellationToken ct = default)
    {
        var query = dbContext.Users.Where(filter ?? new UserFilter());

        var total = await query.CountAsync(ct);
        var results = await query
            .ApplySort(sort ?? new SortOptions<UserSortField>())
            .Skip(paginationParams.Offset)
            .Take(paginationParams.PageSize)
            .Select(PublicUserInfoProjection)
            .ToListAsync(ct);

        return new PaginatedResponse<PublicUserInfo>
        {
            Results = results,
            Offset = paginationParams.Offset,
            Total = total
        };
    }

    /// <inheritdoc />
    public async Task<CurrentUser> UpdateCurrentUserAsync(WriteUserRequest request, CancellationToken ct = default)
    {
        var actor = await actorAccessor.GetActorAsync(ct);
        var currentUserId = actor.User?.Id ?? throw new NoCurrentUserException();

        // Loaded through this context rather than taken from the actor, which another context or a
        // tool may have supplied untracked. Within a request they are the same tracked instance.
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Id == currentUserId, ct)
                   ?? throw new NoCurrentUserException();

        user.DisplayName = request.DisplayName;
        user.NormalizedDisplayName = request.DisplayName.ToLowerInvariant();
        await dbContext.SaveChangesAsync(ct);

        return CurrentUser.FromUser(user);
    }

    /// <summary>
    /// Projects a user to the public model in the query, so that the email is never read.
    /// </summary>
    private static readonly Expression<Func<User, PublicUserInfo>> PublicUserInfoProjection = u => new PublicUserInfo
    {
        Id = u.Id,
        DisplayName = u.DisplayName,
    };
}

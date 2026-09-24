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

    /// <summary>
    /// Projects a user to the public model in the query, so that the email is never read.
    /// </summary>
    private static readonly Expression<Func<User, PublicUserInfo>> PublicUserInfoProjection = u => new PublicUserInfo
    {
        Id = u.Id,
        DisplayName = u.DisplayName,
    };
}

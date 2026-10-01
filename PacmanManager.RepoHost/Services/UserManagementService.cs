using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Models;
using PTrampert.QueryObjects;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Implementation of <see cref="IUserManagementService"/>.
/// </summary>
/// <remarks>
/// The methods that act on the current user first ask <see cref="UserAccessPolicy"/> for the verdict
/// on their own action, through <see cref="CurrentActorAsync"/>. The current user has no anonymous
/// view, so a read the actor's scope does not permit is refused rather than shown less. Listing
/// users and reading one by id serve anonymous routes, and ask nothing.
/// </remarks>
/// <param name="actorAccessor">Supplies the caller for the methods that act on the current user.</param>
/// <param name="accessPolicy">Decides whether the actor's credential may read or change the current user.</param>
/// <param name="dbContext">The database the users are read from.</param>
internal class UserManagementService(
    IActorAccessor actorAccessor,
    UserAccessPolicy accessPolicy,
    PacmanManagerDbContext dbContext) : IUserManagementService
{
    /// <summary>
    /// The current actor, once <paramref name="verdict"/> has allowed it to perform
    /// <paramref name="action"/> on its own user record.
    /// </summary>
    /// <param name="verdict">The <see cref="UserAccessPolicy"/> verdict for the operation.</param>
    /// <param name="action">The action <paramref name="verdict"/> answers for, named in a refusal.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="NoCurrentUserException">The actor has no user.</exception>
    /// <exception cref="InsufficientScopeException">The actor's scope does not permit the action.</exception>
    private async ValueTask<Actor> CurrentActorAsync(Func<Actor, RepositoryAccess> verdict, string action, CancellationToken ct)
    {
        var actor = await actorAccessor.GetActorAsync(ct);

        return verdict(actor) switch
        {
            RepositoryAccess.Allowed => actor,
            RepositoryAccess.Unauthenticated => throw new NoCurrentUserException(),
            // The current user has no anonymous view to fall back to, so a refused read is refused too.
            _ => throw new InsufficientScopeException(ScopeValues.EntityNames.Users, action),
        };
    }

    /// <inheritdoc />
    public async Task<CurrentUser> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var actor = await CurrentActorAsync(accessPolicy.CheckReadCurrent, ScopeValues.ActionNames.Read, ct);
        // A system actor is allowed, but it may have no user to be.
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
        var actor = await CurrentActorAsync(accessPolicy.CheckUpdateCurrent, ScopeValues.ActionNames.Update, ct);
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

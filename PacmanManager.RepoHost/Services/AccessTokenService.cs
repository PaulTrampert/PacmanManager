using Microsoft.EntityFrameworkCore;
using Npgsql;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Models;
using PTrampert.QueryObjects;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Implementation of <see cref="IAccessTokenService"/> backed by the application database.
/// </summary>
/// <remarks>
/// <para>
/// Every read and delete starts from <see cref="OwnAsync"/>, which is the only place in this class
/// that names <see cref="PacmanManagerDbContext.PacmanAccessTokens"/> and which restricts it to the
/// actor's own tokens before returning. Minting adds through the context rather than the
/// <see cref="DbSet{TEntity}"/>, so the rule holds for it too. <c>AccessTokenServiceEnforcementTests</c>
/// asserts both this and that <see cref="UserService"/> is the only other type naming the set.
/// </para>
/// <para>
/// Every method first asks <see cref="AccessTokenAccessPolicy"/> for the verdict on its own action,
/// through <see cref="OwnerAsync"/>, which each caller hands the verdict to apply. There is no
/// anonymous view of tokens, so a read the actor's scope does not permit is refused rather than
/// shown less.
/// </para>
/// <para>
/// Nothing checks for a duplicate name before writing: the unique index on
/// <c>(UserId, NormalizedName)</c> decides, so two concurrent mints of the same name cannot both
/// succeed.
/// </para>
/// </remarks>
/// <param name="dbContext">The database the tokens are stored in.</param>
/// <param name="actorAccessor">Supplies the actor whose tokens are the subject of every method.</param>
/// <param name="accessPolicy">Decides whether the actor's credential may list, mint or revoke tokens.</param>
/// <param name="timeProvider">The clock a minted token's creation time is read from.</param>
/// <param name="logger">Logger for minting and revocation.</param>
internal class AccessTokenService(
    PacmanManagerDbContext dbContext,
    IActorAccessor actorAccessor,
    AccessTokenAccessPolicy accessPolicy,
    TimeProvider timeProvider,
    ILogger<AccessTokenService> logger) : IAccessTokenService
{
    /// <summary>
    /// The user whose tokens are the subject of this unit of work, once <paramref name="verdict"/>
    /// has allowed the actor to perform <paramref name="action"/> on them.
    /// </summary>
    /// <param name="verdict">The <see cref="AccessTokenAccessPolicy"/> verdict for the operation.</param>
    /// <param name="action">The action <paramref name="verdict"/> answers for, named in a refusal.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="NoCurrentUserException">The actor has no user, so owns no tokens.</exception>
    /// <exception cref="InsufficientScopeException">The actor's scope does not permit the action.</exception>
    private async ValueTask<User> OwnerAsync(Func<Actor, RepositoryAccess> verdict, string action, CancellationToken ct)
    {
        var actor = await actorAccessor.GetActorAsync(ct);

        switch (verdict(actor))
        {
            case RepositoryAccess.Allowed:
                break;
            case RepositoryAccess.Unauthenticated:
                throw new NoCurrentUserException();
            default:
                // Tokens have no anonymous view to fall back to, so a refused read is refused too.
                throw new InsufficientScopeException(ScopeValues.EntityNames.Tokens, action);
        }

        // A system actor is allowed, but tokens are always somebody's own, and it may be nobody.
        return actor.User ?? throw new NoCurrentUserException();
    }

    /// <summary>
    /// The current actor's own tokens, once <paramref name="verdict"/> has allowed the actor to
    /// perform <paramref name="action"/> on them. Every read and delete in this class starts here.
    /// </summary>
    /// <param name="verdict">The <see cref="AccessTokenAccessPolicy"/> verdict for the operation.</param>
    /// <param name="action">The action <paramref name="verdict"/> answers for, named in a refusal.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="NoCurrentUserException">The actor has no user, so owns no tokens.</exception>
    /// <exception cref="InsufficientScopeException">The actor's scope does not permit the action.</exception>
    private async ValueTask<IQueryable<PacmanAccessToken>> OwnAsync(
        Func<Actor, RepositoryAccess> verdict,
        string action,
        CancellationToken ct)
    {
        var ownerId = (await OwnerAsync(verdict, action, ct)).Id;
        return dbContext.PacmanAccessTokens.Where(t => t.UserId == ownerId);
    }

    /// <summary>
    /// Whether <paramref name="exception"/> is the database refusing a token because its owner
    /// already has one of the same normalized name, as opposed to any other failure to save.
    /// </summary>
    /// <remarks>
    /// The index is matched by the name the model gives it rather than a literal, so the check
    /// follows the index if its columns change.
    /// </remarks>
    private bool IsNameCollision(DbUpdateException exception)
    {
        var nameIndex = dbContext.Model
            .FindEntityType(typeof(PacmanAccessToken))!
            .GetIndexes()
            .Single(i => i.IsUnique)
            .GetDatabaseName();

        return exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
               && postgres.ConstraintName == nameIndex;
    }

    /// <inheritdoc />
    public async Task<PaginatedResponse<AccessToken>> GetAccessTokensAsync(
        PaginationParams paginationParams,
        AccessTokenFilter? filter = null,
        SortOptions<AccessTokenSortField>? sort = null,
        CancellationToken ct = default)
    {
        // Ownership comes first and the caller's criteria are ANDed onto it, so a filter can only
        // ever remove rows from the actor's own tokens.
        var query = (await OwnAsync(accessPolicy.CheckRead, ScopeValues.ActionNames.Read, ct))
            .Where(filter ?? new AccessTokenFilter());

        var total = await query.CountAsync(ct);
        var results = await query
            .ApplySort(sort ?? new SortOptions<AccessTokenSortField>())
            .Skip(paginationParams.Offset)
            .Take(paginationParams.PageSize)
            .Select(AccessToken.Projection)
            .ToListAsync(ct);

        return new PaginatedResponse<AccessToken>
        {
            Results = results,
            Offset = paginationParams.Offset,
            Total = total
        };
    }

    /// <inheritdoc />
    public async Task<CreatedAccessToken> CreateAccessTokenAsync(CreateAccessTokenRequest request, CancellationToken ct = default)
    {
        var owner = await OwnerAsync(accessPolicy.CheckCreate, ScopeValues.ActionNames.Create, ct);

        // Generated here and nowhere else: the fast, unsalted hash stored below is correct only for
        // a secret from a CSPRNG, never for a value a caller supplied.
        var secret = AccessTokenFormat.GenerateSecret();
        var token = new PacmanAccessToken
        {
            Id = Guid.CreateVersion7(),
            UserId = owner.Id,
            Name = request.Name,
            // Written with Name and from Name, and nowhere else. The invariant culture, never the
            // current one, so that the same name normalizes the same way on every host.
            NormalizedName = request.Name.ToLowerInvariant(),
            TokenHash = AccessTokenFormat.HashSecret(secret),
            CreatedAt = timeProvider.GetUtcNow(),
            ExpiresAt = request.ExpiresAt,
        };

        dbContext.Add(token);
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (IsNameCollision(e))
        {
            // Nothing later in this unit of work should try to save the refused row again.
            dbContext.Entry(token).State = EntityState.Detached;
            throw new ItemExistsException("An access token with this name already exists.", e);
        }

        logger.LogInformation("Access token {TokenId} minted", token.Id);
        return CreatedAccessToken.FromPacmanAccessToken(token, secret);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAccessTokenAsync(Guid tokenId, CancellationToken ct = default)
    {
        // Another user's token is simply not in the actor's own set, so it is the same miss as a
        // token that does not exist: saying "forbidden" would confirm that the id names a token.
        var token = await (await OwnAsync(accessPolicy.CheckDelete, ScopeValues.ActionNames.Delete, ct))
            .SingleOrDefaultAsync(t => t.Id == tokenId, ct);
        if (token is null)
        {
            return false;
        }

        dbContext.Remove(token);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Access token {TokenId} revoked", tokenId);
        return true;
    }
}

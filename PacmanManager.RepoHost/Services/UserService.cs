using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Config;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Implementation of <see cref="IUserService"/> for managing users and their identity links.
/// </summary>
/// <remarks>
/// Not actor-scoped: it answers "which user is this?" during authentication, before any actor
/// exists, and must not take an <see cref="IActorAccessor"/>.
/// </remarks>
/// <param name="dbContext">The database context used for user and mapping operations.</param>
/// <param name="accessTokenConfig">Configuration for access token verification.</param>
/// <param name="timeProvider">The clock expiry and <see cref="PacmanAccessToken.LastUsedAt"/> are judged against.</param>
/// <param name="logger">Logger for access token use.</param>
public class UserService(
    PacmanManagerDbContext dbContext,
    IOptions<AccessTokenConfig> accessTokenConfig,
    TimeProvider timeProvider,
    ILogger<UserService> logger) : IUserService
{
    /// <summary>
    /// Why an access token failed to verify. Logged, never returned: the caller sees only <c>null</c>.
    /// </summary>
    private enum AccessTokenFailure
    {
        /// <summary>The username or password is not in the access token format.</summary>
        Malformed,

        /// <summary>No token has both the identifier and the secret. An unknown identifier and a wrong secret are indistinguishable.</summary>
        NoMatch,

        /// <summary>The token matched but is past its expiry.</summary>
        Expired
    }

    /// <inheritdoc />
    public Task<User?> GetUserByExternalIdAsync(string authority, string subject, CancellationToken ct = default)
    {
        return dbContext.UserMappings
            .Where(m => m.ExternalAuthority == authority && m.ExternalId == subject)
            .Select(m => m.User)
            .SingleOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public Task<User?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        return dbContext.Users
            .Where(u => u.Email == email)
            .SingleOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<User> CreateUserAsync(User user, CancellationToken ct = default)
    {
        var result = await dbContext.Users.AddAsync(user, ct);
        await dbContext.SaveChangesAsync(ct);
        return result.Entity;
    }

    /// <inheritdoc />
    public async Task<User> LinkToIdentityAsync(
        User user, 
        string authority, 
        string subject,
        CancellationToken ct = default)
    {
        await dbContext.UserMappings.AddAsync(new ExternalProviderUserMapping
        {
            ExternalAuthority = authority,
            ExternalId = subject,
            User = user
        }, ct);

        await dbContext.SaveChangesAsync(ct);

        return user;
    }

    /// <summary>
    /// How many times <see cref="EnsureUserLinkedAsync"/> tries before letting a collision through.
    /// </summary>
    /// <remarks>
    /// A collision means a concurrent request committed the row this one tried to write, so the next
    /// attempt reads it. Two attempts cover a single race; the third covers the user and the mapping
    /// being won by different requests, as when two identities share an email address.
    /// </remarks>
    private const int MaxLinkAttempts = 3;

    /// <inheritdoc />
    public async Task<User> EnsureUserLinkedAsync(string email, string displayName, string authority, string subject, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            var added = new List<object>();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var user = await GetUserByEmailAsync(email, ct);
                if (user == null)
                {
                    user = new User
                    {
                        DisplayName = displayName,
                        NormalizedDisplayName = displayName.ToLowerInvariant(),
                        Email = email
                    };
                    dbContext.Users.Add(user);
                    added.Add(user);
                }

                var existingMapping = await dbContext.UserMappings
                    .AnyAsync(m => m.ExternalAuthority == authority && m.ExternalId == subject, ct);
                if (!existingMapping)
                {
                    var mapping = new ExternalProviderUserMapping
                    {
                        ExternalAuthority = authority,
                        ExternalId = subject,
                        User = user
                    };
                    dbContext.UserMappings.Add(mapping);
                    added.Add(mapping);
                }

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return user;
            }
            catch (DbUpdateException e) when (attempt < MaxLinkAttempts && IsIdentityCollision(e))
            {
                // Another request created the user or the mapping between the reads above and the
                // write. Postgres has aborted this transaction, so start again with a new one; the
                // rows this attempt added must not be saved again.
                await transaction.RollbackAsync(ct);
                foreach (var entity in added)
                {
                    dbContext.Entry(entity).State = EntityState.Detached;
                }

                logger.LogInformation(e,
                    "Concurrent sign-in created the user for {Authority} {Subject} first; retrying (attempt {Attempt})",
                    authority, subject, attempt);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="exception"/> is the database refusing a user or identity mapping
    /// because a concurrent request already created it, as opposed to any other failure to save.
    /// </summary>
    /// <remarks>
    /// The indexes are matched by the names the model gives them rather than literals
    /// (<c>IX_Users_Email</c>, <c>IX_UserMappings_ExternalAuthority_ExternalId</c>), so the check
    /// follows the indexes if their columns change.
    /// </remarks>
    private bool IsIdentityCollision(DbUpdateException exception)
    {
        if (exception.InnerException is not PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            return false;
        }

        return postgres.ConstraintName == UniqueIndexName<User>()
               || postgres.ConstraintName == UniqueIndexName<ExternalProviderUserMapping>();
    }

    private string? UniqueIndexName<TEntity>() =>
        dbContext.Model
            .FindEntityType(typeof(TEntity))!
            .GetIndexes()
            .Single(i => i.IsUnique)
            .GetDatabaseName();

    /// <inheritdoc />
    public async Task<User?> GetUserByAccessTokenAsync(string username, string password, CancellationToken ct = default)
    {
        // Never log the username raw: a client that swaps the two fields puts the secret in it.
        var hasTokenId = AccessTokenFormat.TryParseUsername(username, out var tokenId);
        if (!AccessTokenFormat.TryHashSecret(password, out var presentedHash) || !hasTokenId)
        {
            LogFailure(hasTokenId ? tokenId : null, AccessTokenFailure.Malformed);
            return null;
        }

        var token = await FindAccessTokenAsync(tokenId, presentedHash, ct);
        if (token == null)
        {
            LogFailure(tokenId, AccessTokenFailure.NoMatch);
            return null;
        }

        var now = timeProvider.GetUtcNow();
        if (token.ExpiresAt is { } expiresAt && expiresAt <= now)
        {
            LogFailure(tokenId, AccessTokenFailure.Expired);
            return null;
        }

        await RecordUseAsync(token, now, ct);

        logger.LogInformation("Access token {TokenId} verified", tokenId);
        return token.User;
    }

    /// <summary>
    /// Finds the token matching both the identifier and the hash of the presented secret, in one
    /// query.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The single query predicated on both halves is a requirement, not an optimisation.</b> An
    /// unknown identifier and a wrong secret must be the same outcome, reached by the same path, so
    /// that the time a failure takes does not say whether a token id exists. Fetching by id and
    /// comparing the hash in memory gives that away, and is not an acceptable implementation.
    /// </para>
    /// <para>
    /// There is deliberately no <c>CryptographicOperations.FixedTimeEquals</c>, and the comparison
    /// stays in the database, which does not compare in constant time. That is safe because it
    /// compares <i>hashes</i>: a caller controls the secret, not its SHA-256, so learning how many
    /// leading bytes of the hash matched tells them nothing about which secret to try next. Do not
    /// "fix" this by moving the comparison into memory, and never by storing the secret.
    /// </para>
    /// <para>
    /// Expiry is checked by the caller on the returned row rather than here, so that an expired token
    /// can be logged as such.
    /// </para>
    /// </remarks>
    private Task<PacmanAccessToken?> FindAccessTokenAsync(Guid tokenId, string presentedHash, CancellationToken ct)
    {
        return dbContext.PacmanAccessTokens
            .Where(t => t.Id == tokenId && t.TokenHash == presentedHash)
            .Include(t => t.User)
            .SingleOrDefaultAsync(ct);
    }

    /// <summary>
    /// Writes <see cref="PacmanAccessToken.LastUsedAt"/> if it is older than the configured
    /// resolution. Best-effort: a failure is logged and never fails the verification.
    /// </summary>
    private async Task RecordUseAsync(PacmanAccessToken token, DateTimeOffset now, CancellationToken ct)
    {
        if (token.LastUsedAt is { } lastUsedAt
            && now - lastUsedAt < accessTokenConfig.Value.LastUsedAtResolution)
        {
            return;
        }

        var entry = dbContext.Entry(token);
        try
        {
            token.LastUsedAt = now;
            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception e) when (e is DbUpdateException or DbException)
        {
            logger.LogWarning(e, "Failed to record use of access token {TokenId}", token.Id);
        }
        finally
        {
            // Whether or not the write landed, nothing later in this unit of work should save it.
            entry.State = EntityState.Unchanged;
        }
    }

    private void LogFailure(Guid? tokenId, AccessTokenFailure reason)
    {
        if (tokenId is { } id)
        {
            logger.LogWarning("Access token {TokenId} failed verification: {Reason}", id, reason);
        }
        else
        {
            logger.LogWarning("Access token failed verification: {Reason}", reason);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Derives the current <see cref="Actor"/> from the authenticated principal on the HTTP request.
/// </summary>
/// <param name="httpContext">Supplies the principal of the current request.</param>
/// <param name="dbContext">The database the user behind the principal is read from.</param>
/// <param name="logger">Logger for warnings about a principal that names no usable user.</param>
public class HttpContextActorAccessor(
    IHttpContextAccessor httpContext,
    PacmanManagerDbContext dbContext,
    ILogger<HttpContextActorAccessor> logger) : IActorAccessor
{
    private Actor? _cached;

    /// <inheritdoc />
    public async ValueTask<Actor> GetActorAsync(CancellationToken ct = default)
    {
        // This service is scoped to the request, so the lookup only needs to happen once even
        // though a single operation may consult the actor several times.
        if (_cached is not null)
        {
            return _cached;
        }

        var user = await FindUserAsync(ct);
        return _cached = user is null ? Actor.Anonymous : Actor.For(user, ParseScope());
    }

    /// <summary>
    /// Parses the principal's <c>scope</c> claim. This is the one place it is read. Whatever it parses
    /// to is the actor's scope, an empty one included: a credential carrying none of our values keeps
    /// its user, and may do only what a stranger may.
    /// </summary>
    private ActorScope ParseScope()
    {
        // RFC 6749 makes scope one space-delimited string, but joining tolerates a provider that
        // issues it as repeated claims instead.
        var values = httpContext.HttpContext?.User.Claims
            .Where(c => c.Type == AuthnConstants.ScopeClaimType)
            .Select(c => c.Value);
        return ActorScope.Parse(values is null ? null : string.Join(' ', values), logger);
    }

    private Task<User?> FindUserAsync(CancellationToken ct)
    {
        var userIdClaim = httpContext.HttpContext?.User.Claims
            .SingleOrDefault(c => c.Type == AuthnConstants.AppUserIdClaimType);
        if (userIdClaim == null)
        {
            logger.LogWarning($"No {nameof(userIdClaim)} found.");
            return Task.FromResult<User?>(null);
        }

        if (!Guid.TryParse(userIdClaim.Value, out var userId))
        {
            logger.LogWarning("Could not parse user id from '{@UserIdClaimValue}'", userIdClaim.Value);
            return Task.FromResult<User?>(null);
        }

        return dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
    }
}

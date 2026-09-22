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
        return _cached = user is null ? Actor.Anonymous : Actor.For(user);
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

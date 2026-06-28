using Microsoft.EntityFrameworkCore;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Exceptions;

namespace PacmanManager.RepoHost.Services
{
    /// <summary>
    /// Implementation of <see cref="ICurrentUserService"/> that relies on <see cref="IHttpContextAccessor"/>.
    /// </summary>
    /// <param name="httpContext">The current http context.</param>
    /// <param name="dbContext">The database</param>
    /// <param name="logger">Logger for logging warnings.</param>
    public class CurrentUserService(IHttpContextAccessor httpContext, PacmanManagerDbContext dbContext, ILogger<CurrentUserService> logger) : ICurrentUserService
    {
        /// <inheritdoc />
        public Task<User?> GetCurrentUserAsync(CancellationToken ct = default)
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

        /// <inheritdoc />
        public async Task<User> RequireCurrentUserAsync(CancellationToken ct = default)
        {
            var user = await GetCurrentUserAsync(ct);
        
            return user ?? throw new NoCurrentUserException();
        }
    }
}
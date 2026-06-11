using Microsoft.EntityFrameworkCore;
using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Services;

public class UserService(PacmanManagerDbContext dbContext, ILogger<UserService> logger) : IUserService
{
    public Task<User?> GetUserByExternalIdAsync(string authority, string subject, CancellationToken ct = default)
    {
        return dbContext.UserMappings
            .Where(m => m.ExternalAuthority == authority && m.ExternalId == subject)
            .Select(m => m.User)
            .SingleOrDefaultAsync(ct);
    }

    public Task<User?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        return dbContext.Users
            .Where(u => u.Email == email)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<User> CreateUserAsync(User user, CancellationToken ct = default)
    {
        var result = await dbContext.Users.AddAsync(user, ct);
        await dbContext.SaveChangesAsync(ct);
        return result.Entity;
    }

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
}
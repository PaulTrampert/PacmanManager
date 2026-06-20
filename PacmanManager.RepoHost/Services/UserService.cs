using Microsoft.EntityFrameworkCore;
using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Implementation of <see cref="IUserService"/> for managing users and their identity links.
/// </summary>
/// <param name="dbContext">The database context used for user and mapping operations.</param>
public class UserService(PacmanManagerDbContext dbContext) : IUserService
{
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

    /// <inheritdoc />
    public async Task<User> EnsureUserLinkedAsync(string email, string displayName, string authority, string subject, CancellationToken ct = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var user = await GetUserByEmailAsync(email, ct);
            if (user == null)
            {
                user = await CreateUserAsync(new User
                {
                    DisplayName = displayName,
                    Email = email
                }, ct);
            }

            // Check if already linked to avoid unique constraint violation on ExternalProviderUserMapping
            var existingMapping = await dbContext.UserMappings
                .AnyAsync(m => m.ExternalAuthority == authority && m.ExternalId == subject, ct);

            if (!existingMapping)
            {
                await LinkToIdentityAsync(user, authority, subject, ct);
            }

            await transaction.CommitAsync(ct);
            return user;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Models;

public record PublicUserInfo
{
    public required Guid Id { get; init; }
    
    public required string DisplayName { get; init; }

    public static PublicUserInfo FromUser(User user)
    {
        return new PublicUserInfo
        {
            Id = user.Id,
            DisplayName = user.DisplayName
        };
    }
};
using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The authenticated caller's own user record, including the parts that are not public.
/// </summary>
/// <remarks>
/// Deliberately a separate model from <see cref="PublicUserInfo"/>, so that the model every anonymous
/// route returns has no field capable of carrying an email. Return this only to the user it describes.
/// </remarks>
public record CurrentUser
{
    /// <summary>
    /// The user's id.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The name the user has chosen to be shown by. Not unique.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The user's email address.
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// Projects a <see cref="User"/> entity onto this model.
    /// </summary>
    /// <param name="user">The user to project.</param>
    /// <returns>A new <see cref="CurrentUser"/> describing <paramref name="user"/>.</returns>
    public static CurrentUser FromUser(User user)
    {
        return new CurrentUser
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
        };
    }
}

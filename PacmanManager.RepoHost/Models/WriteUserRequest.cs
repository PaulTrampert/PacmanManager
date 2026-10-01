using System.ComponentModel.DataAnnotations;
using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The writable surface of the caller's own user record. <c>PATCH /api/v1/users/me</c> takes it as an
/// <c>IPatchObject&lt;WriteUserRequest&gt;</c>, so every property is optional on the wire.
/// </summary>
/// <remarks>
/// The validation attributes survive onto the generated patch object and apply only to a property the
/// body names: an omitted <see cref="DisplayName"/> leaves the name as it is, while an explicit
/// <c>null</c> fails <see cref="RequiredAttribute"/> rather than clearing it.
/// </remarks>
public record WriteUserRequest
{
    /// <summary>
    /// The name the user has chosen to be shown by. Not unique.
    /// </summary>
    [Required]
    [MaxLength(UserValidationConstants.DisplayNameMaxLength)]
    public required string DisplayName { get; init; }

    /// <summary>
    /// Projects a user's present values onto this model, as the target a patch is applied to.
    /// </summary>
    /// <param name="user">The user whose writable values to copy.</param>
    /// <returns>A new <see cref="WriteUserRequest"/> holding <paramref name="user"/>'s present values.</returns>
    public static WriteUserRequest FromCurrentUser(CurrentUser user)
    {
        return new WriteUserRequest
        {
            DisplayName = user.DisplayName,
        };
    }
}

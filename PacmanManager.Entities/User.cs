using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace PacmanManager.Entities;

[Index(nameof(Email), IsUnique = true)]
public record User
{
    [Key] 
    public Guid Id { get; init; } = Guid.CreateVersion7();
    
    [Required]
    [MaxLength(UserValidationConstants.DisplayNameMaxLength)]
    public required string DisplayName { get; set; }

    /// <summary>
    /// <see cref="DisplayName"/> lowered with <see cref="string.ToLowerInvariant"/>, used for matching
    /// and never shown. Written from <see cref="DisplayName"/>, in the same statement that writes
    /// <see cref="DisplayName"/>; nothing else sets it, and no wire model carries it.
    /// </summary>
    /// <remarks>
    /// The invariant culture is required: the current culture's lowercasing differs between hosts, and
    /// a normalization that depends on the host is not one. There is no index: display names are not
    /// unique, and a substring match cannot use a b-tree index.
    /// </remarks>
    [Required]
    [MaxLength(UserValidationConstants.NormalizedDisplayNameMaxLength)]
    public required string NormalizedDisplayName { get; set; }
    
    [MaxLength(UserValidationConstants.EmailMaxLength)]
    [RegularExpression(UserValidationConstants.EmailRegex)]
    public required string Email { get; set; }
};
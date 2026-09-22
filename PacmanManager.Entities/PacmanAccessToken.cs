using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace PacmanManager.Entities;

/// <summary>
/// A long-lived credential a user mints for a non-interactive client such as <c>pacman</c>, presented
/// as the two halves of an HTTP Basic credential. Only a hash of the secret is stored, so the secret
/// is shown once, when the token is minted, and is never retrievable again.
/// </summary>
/// <remarks>
/// The unique index on <c>(UserId, NormalizedName)</c> makes token names unique per user without
/// regard to case: <c>laptop</c> and <c>Laptop</c> are the same name. It is on
/// <see cref="NormalizedName"/> rather than <see cref="Name"/> because a case-insensitive index cannot
/// be declared with data annotations, and a stored lowered copy behaves the same under Postgres and
/// the in-memory provider.
/// </remarks>
[Index(nameof(UserId), nameof(NormalizedName), IsUnique = true)]
public record PacmanAccessToken
{
    /// <summary>
    /// Internal database Id for the token. It is also the token's lookup key: the Basic username is
    /// this value, prefixed, and it is not secret.
    /// </summary>
    [Key]
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// Id of the user the token authenticates as.
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// The user the token authenticates as. Deleting the user deletes their tokens.
    /// </summary>
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public virtual User User { get; set; } = null!;

    /// <summary>
    /// A label the user chose, so that a list of tokens is legible. Kept exactly as entered.
    /// </summary>
    [Required]
    [MaxLength(AccessTokenValidationConstants.NameMaxLength)]
    [MinLength(AccessTokenValidationConstants.NameMinLength)]
    public required string Name { get; set; }

    /// <summary>
    /// <see cref="Name"/> lowered with <see cref="string.ToLowerInvariant"/>, used for indexing and
    /// matching and never shown. Written by the service from <see cref="Name"/>, in the same statement
    /// that writes <see cref="Name"/>; nothing else sets it, and no wire model carries it.
    /// </summary>
    /// <remarks>
    /// The invariant culture is required: the current culture's lowercasing differs between hosts, and
    /// a normalization that depends on the host is not one.
    /// </remarks>
    [Required]
    [MaxLength(AccessTokenValidationConstants.NormalizedNameMaxLength)]
    public required string NormalizedName { get; set; }

    /// <summary>
    /// Base64 SHA-256 of the token's secret. The secret itself is never stored.
    /// </summary>
    [Required]
    [MaxLength(AccessTokenValidationConstants.TokenHashMaxLength)]
    public required string TokenHash { get; set; }

    /// <summary>
    /// When the token was minted.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the token stops authenticating, or <c>null</c> if it never expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// When the token was last used to authenticate. Coarsely maintained: it is written only when the
    /// stored value is older than a configured resolution, so it may lag the true last use.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; set; }
}

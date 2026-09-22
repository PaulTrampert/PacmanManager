using System.ComponentModel.DataAnnotations;
using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Request model for minting an access token.
/// </summary>
public record CreateAccessTokenRequest : IValidatableObject
{
    /// <summary>
    /// A label for the token, so that a list of tokens is legible. Unique per user without regard
    /// to case: <c>laptop</c> and <c>Laptop</c> are the same name.
    /// </summary>
    [Required]
    [MaxLength(AccessTokenValidationConstants.NameMaxLength)]
    [MinLength(AccessTokenValidationConstants.NameMinLength)]
    public required string Name { get; init; }

    /// <summary>
    /// When the token stops authenticating. Must be in the future when present.
    /// <b>Omitted or <c>null</c> means the token never expires</b>; there is no default lifetime and
    /// no maximum.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Rejects an <see cref="ExpiresAt"/> that is not in the future, so that a past date is a
    /// <c>400</c> rather than a token that is dead on arrival.
    /// </summary>
    /// <param name="validationContext">
    /// The validation context. When it can supply a <see cref="TimeProvider"/>, that is the clock the
    /// date is judged against; otherwise <see cref="TimeProvider.System"/> is.
    /// </param>
    /// <returns>The validation failures, if any.</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var clock = validationContext.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
        if (ExpiresAt is { } expiresAt && expiresAt <= clock.GetUtcNow())
        {
            yield return new ValidationResult(
                "The expiry must be in the future. Omit it for a token that never expires.",
                [nameof(ExpiresAt)]);
        }
    }
}

namespace PacmanManager.Entities;

/// <summary>
/// Validation limits for <see cref="PacmanAccessToken"/>. Shared so that the entity, the API models and
/// their tests all agree on the same numbers instead of repeating literals at each use site.
/// </summary>
public static class AccessTokenValidationConstants
{
    /// <summary>
    /// Maximum length of a token name.
    /// </summary>
    public const int NameMaxLength = 255;

    /// <summary>
    /// Minimum length of a token name.
    /// </summary>
    public const int NameMinLength = 1;

    /// <summary>
    /// Maximum length of a token's normalized name. It is the name lowered, so it shares
    /// <see cref="NameMaxLength"/>.
    /// </summary>
    public const int NormalizedNameMaxLength = NameMaxLength;

    /// <summary>
    /// Length of a Base64 encoded SHA-256 hash: 32 bytes encode to 44 characters, padding included.
    /// </summary>
    public const int TokenHashLength = 44;
}

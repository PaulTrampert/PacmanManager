using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// A token that has just been minted: <see cref="AccessToken"/>'s fields plus the secret, and the
/// only model that ever carries one.
/// </summary>
/// <remarks>
/// A type of its own rather than a nullable <c>Secret</c> on <see cref="AccessToken"/>, because a
/// field that is only sometimes populated is one that will one day be populated by accident, and
/// here that accident would put a live credential in a listing. It does not derive from
/// <see cref="AccessToken"/> either, so that it can never be passed where a listing entry is
/// expected.
/// </remarks>
public record CreatedAccessToken
{
    /// <inheritdoc cref="AccessToken.Id"/>
    public required Guid Id { get; init; }

    /// <inheritdoc cref="AccessToken.Username"/>
    public string Username => AccessTokenFormat.FormatUsername(Id);

    /// <inheritdoc cref="AccessToken.Name"/>
    public required string Name { get; init; }

    /// <inheritdoc cref="AccessToken.CreatedAt"/>
    public DateTimeOffset CreatedAt { get; init; }

    /// <inheritdoc cref="AccessToken.ExpiresAt"/>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <inheritdoc cref="AccessToken.LastUsedAt"/>
    public DateTimeOffset? LastUsedAt { get; init; }

    /// <summary>
    /// The Basic password a client presents with this token, <c>pms_{secret}</c>. Returned once,
    /// here, and never retrievable again: only its hash is stored.
    /// </summary>
    public required string Secret { get; init; }

    /// <summary>
    /// Creates the model for a token that has just been minted.
    /// </summary>
    /// <param name="token">The stored token.</param>
    /// <param name="secret">The secret whose hash <paramref name="token"/> stores.</param>
    /// <returns>The token, secret included.</returns>
    public static CreatedAccessToken FromPacmanAccessToken(PacmanAccessToken token, string secret) => new()
    {
        Id = token.Id,
        Name = token.Name,
        CreatedAt = token.CreatedAt,
        ExpiresAt = token.ExpiresAt,
        LastUsedAt = token.LastUsedAt,
        Secret = secret,
    };
}

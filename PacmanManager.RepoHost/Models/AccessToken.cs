using System.Linq.Expressions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// An access token, as its owner sees it in a listing. Carries no secret: the secret is shown once,
/// on <see cref="CreatedAccessToken"/>, and only its hash is kept.
/// </summary>
/// <remarks>
/// The wire model keeps the unprefixed name, as <see cref="Repository"/> does; the <c>Pacman</c>
/// prefix on <see cref="PacmanAccessToken"/> distinguishes the storage type. The normalized name is
/// never shown, so it is not here.
/// </remarks>
public record AccessToken
{
    /// <summary>
    /// The token's id, which also names it in <c>DELETE /api/v1/users/me/tokens/{tokenId}</c>.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The Basic username a client presents with this token, <c>pmt_{id:N}</c>. Derived from
    /// <see cref="Id"/> rather than stored, and given here so that a client never has to know how
    /// one is spelled from the other.
    /// </summary>
    public string Username => AccessTokenFormat.FormatUsername(Id);

    /// <summary>
    /// The label the user chose for the token, exactly as entered.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// When the token was minted.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the token stops authenticating, or <c>null</c> if it never expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// When the token was last used to authenticate, or <c>null</c> if it never has been. Coarse by
    /// design: it is written at most once per configured resolution, one hour by default, so it may
    /// lag the true last use by up to that long.
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; init; }

    /// <summary>
    /// Projects the underlying db object onto this model inside a query, so that the token hash and
    /// the normalized name are never fetched.
    /// </summary>
    public static Expression<Func<PacmanAccessToken, AccessToken>> Projection => token => new AccessToken
    {
        Id = token.Id,
        Name = token.Name,
        CreatedAt = token.CreatedAt,
        ExpiresAt = token.ExpiresAt,
        LastUsedAt = token.LastUsedAt,
    };
}

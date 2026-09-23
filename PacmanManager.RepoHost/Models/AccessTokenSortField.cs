namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The properties an access token listing may be ordered by.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CreatedAt"/> is first because <see cref="SortOptions{TSortFields}"/> takes the first
/// member as its default, so reordering this enum changes what an unsorted request returns. A
/// listing nobody has ordered comes back newest first.
/// </para>
/// <para>
/// There is no filter on expiry: sorting by <see cref="ExpiresAt"/> puts the tokens nearest to
/// lapsing together, which is the question a user actually asks.
/// </para>
/// </remarks>
public enum AccessTokenSortField
{
    /// <summary>When the token was minted.</summary>
    [DefaultSortDirection(SortDirection.Descending)]
    CreatedAt,

    /// <summary>The token's name, ignoring case.</summary>
    [DefaultSortDirection(SortDirection.Ascending)]
    Name,

    /// <summary>When the token expires.</summary>
    ExpiresAt,

    /// <summary>When the token was last used.</summary>
    LastUsedAt,
}

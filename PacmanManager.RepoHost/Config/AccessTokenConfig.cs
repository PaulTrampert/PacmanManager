using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Config;

/// <summary>
/// Configurable behaviour of access token verification.
/// </summary>
public class AccessTokenConfig
{
    /// <summary>
    /// Config section to find this object in.
    /// </summary>
    public const string Section = "AccessTokens";

    /// <summary>
    /// The default <see cref="LastUsedAtResolution"/>: one hour.
    /// </summary>
    public static readonly TimeSpan DefaultLastUsedAtResolution = TimeSpan.FromHours(1);

    /// <summary>
    /// How stale <see cref="PacmanAccessToken.LastUsedAt"/> must be before a verification writes it
    /// again. Bounds the column to at most one write per token per resolution, rather than one per
    /// request, so it may lag a token's true last use by up to this much.
    /// </summary>
    public TimeSpan LastUsedAtResolution { get; set; } = DefaultLastUsedAtResolution;
}

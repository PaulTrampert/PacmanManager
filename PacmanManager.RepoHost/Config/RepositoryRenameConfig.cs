using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Config;

/// <summary>
/// How long a name a repository was renamed away from keeps working, and how long it stays reserved.
/// </summary>
public class RepositoryRenameConfig
{
    /// <summary>
    /// Config section to find this object in.
    /// </summary>
    public const string Section = "RepositoryRenames";

    /// <summary>
    /// The default <see cref="RedirectWindow"/>: 30 days.
    /// </summary>
    public static readonly TimeSpan DefaultRedirectWindow = TimeSpan.FromDays(30);

    /// <summary>
    /// The default <see cref="HoldWindow"/>: 30 days.
    /// </summary>
    public static readonly TimeSpan DefaultHoldWindow = TimeSpan.FromDays(30);

    /// <summary>
    /// The default <see cref="LastRequestedAtResolution"/>: one hour, which is invisible against a
    /// 30-day hold.
    /// </summary>
    public static readonly TimeSpan DefaultLastRequestedAtResolution = TimeSpan.FromHours(1);

    /// <summary>
    /// How long after a rename the old name redirects to the repository's current name. After this
    /// it answers <c>410 Gone</c>.
    /// </summary>
    public TimeSpan RedirectWindow { get; set; } = DefaultRedirectWindow;

    /// <summary>
    /// How long after the most recent request for a retired name it stays reserved. A name is
    /// released once it has stopped redirecting and nothing has asked for it for this long.
    /// </summary>
    public TimeSpan HoldWindow { get; set; } = DefaultHoldWindow;

    /// <summary>
    /// How stale <see cref="RetiredRepositoryName.LastRequestedAt"/> must be before a request writes
    /// it again. Bounds the column to at most one write per retired name per resolution, rather than
    /// one per request, so the hold may be up to this much shorter than <see cref="HoldWindow"/>
    /// after the true last request.
    /// </summary>
    public TimeSpan LastRequestedAtResolution { get; set; } = DefaultLastRequestedAtResolution;
}

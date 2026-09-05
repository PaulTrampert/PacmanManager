using System.ComponentModel.DataAnnotations;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Caller-supplied criteria for narrowing a repository listing.
/// </summary>
/// <remarks>
/// Every member of this record can only ever remove repositories from a result set. Visibility is
/// decided separately and applied unconditionally, so no combination of these values can widen
/// what a caller is able to see.
/// </remarks>
public record RepositoryFilter
{
    /// <summary>
    /// Matches repositories whose name contains this value.
    /// </summary>
    [MaxLength(255)]
    public string? NameContains { get; init; }

    /// <summary>
    /// Matches repositories built for this architecture.
    /// </summary>
    [AllowedValues(null, "x86_64", "any")]
    public string? Architecture { get; init; }

    /// <summary>
    /// Matches only public repositories when true, or only private ones when false. Omit for both.
    /// </summary>
    /// <remarks>
    /// Requesting private repositories does not reveal anyone else's; an anonymous caller asking
    /// for <c>isPublic=false</c> gets an empty page, and an authenticated one gets only their own.
    /// </remarks>
    public bool? IsPublic { get; init; }

    /// <summary>
    /// Matches repositories belonging to this owner.
    /// </summary>
    public Guid? OwnerId { get; init; }

    /// <summary>
    /// Matches only repositories belonging to the calling user. Yields nothing for an anonymous
    /// caller. Convenient for clients that do not know their own user id.
    /// </summary>
    public bool MineOnly { get; init; }
}

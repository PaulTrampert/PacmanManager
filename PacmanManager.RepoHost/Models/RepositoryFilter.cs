using System.ComponentModel.DataAnnotations;
using PacmanManager.Entities;
using PTrampert.QueryObjects.Attributes;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Caller-supplied criteria for narrowing a repository listing.
/// </summary>
/// <remarks>
/// <para>
/// Every member of this record can only ever remove repositories from a result set. Visibility is
/// decided separately and applied unconditionally, so no combination of these values can widen
/// what a caller is able to see.
/// </para>
/// <para>
/// The query attributes say how each criterion narrows the query, and PTrampert.QueryObjects
/// turns them into the predicate. A criterion left unset contributes nothing, so the declaration
/// below is the whole story for every member but <see cref="MineOnly"/>, which needs the caller's
/// identity and is resolved where that is known.
/// </para>
/// </remarks>
public record RepositoryFilter
{
    /// <summary>
    /// Matches repositories whose name contains this value.
    /// </summary>
    [MaxLength(255)]
    [StringContainsQuery(nameof(PacmanRepository.Name))]
    public string? NameContains { get; init; }

    /// <summary>
    /// Matches repositories built for this architecture.
    /// </summary>
    [AllowedValues(null, "x86_64", "any")]
    [EqualsQuery]
    public string? Architecture { get; init; }

    /// <summary>
    /// Matches only public repositories when true, or only private ones when false. Omit for both.
    /// </summary>
    /// <remarks>
    /// Requesting private repositories does not reveal anyone else's; an anonymous caller asking
    /// for <c>isPublic=false</c> gets an empty page, and an authenticated one gets only their own.
    /// </remarks>
    [EqualsQuery]
    public bool? IsPublic { get; init; }

    /// <summary>
    /// Matches repositories belonging to this owner.
    /// </summary>
    [EqualsQuery]
    public Guid? OwnerId { get; init; }

    /// <summary>
    /// Matches only repositories belonging to the calling user. Yields nothing for an anonymous
    /// caller. Convenient for clients that do not know their own user id.
    /// </summary>
    public bool MineOnly { get; init; }
}

using PacmanManager.Entities;
using PTrampert.QueryObjects.Attributes;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Caller-supplied criteria for narrowing a job listing.
/// </summary>
/// <remarks>
/// The listing is already restricted to the jobs the caller may see, by
/// <see cref="Services.JobAccessPolicy.VisibleTo"/>, before this is applied, and every member here
/// can only remove rows from it.
/// </remarks>
public record JobFilter
{
    /// <summary>
    /// Matches jobs in this state.
    /// </summary>
    [EqualsQuery]
    public JobState? State { get; init; }
}

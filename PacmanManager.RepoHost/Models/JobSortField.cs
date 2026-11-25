namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The properties a job listing may be ordered by.
/// </summary>
/// <remarks>
/// <see cref="CreatedAt"/> is first because <see cref="SortOptions{TSortFields}"/> takes the first
/// member as its default, so reordering this enum changes what an unsorted request returns. A
/// listing nobody has ordered comes back newest first.
/// </remarks>
public enum JobSortField
{
    /// <summary>When the job was scheduled.</summary>
    [DefaultSortDirection(SortDirection.Descending)]
    CreatedAt,
}

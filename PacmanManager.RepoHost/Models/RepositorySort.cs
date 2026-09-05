using System.ComponentModel;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Caller-supplied ordering for a repository listing.
/// </summary>
/// <remarks>
/// <para>
/// Ordering is kept apart from <see cref="RepositoryFilter"/> because the two answer different
/// questions: a filter decides which repositories are in the result set, this decides only the
/// sequence they come back in. Nothing here can change which repositories a caller sees.
/// </para>
/// <para>
/// What to order by and which way round are separate choices, so they are separate properties.
/// Folding them together would mean a new member for every combination each time a sortable
/// property is added.
/// </para>
/// </remarks>
public record RepositorySort
{
    /// <summary>
    /// The property to order by.
    /// </summary>
    [DefaultValue(RepositorySortField.Created)]
    public RepositorySortField SortBy { get; init; } = RepositorySortField.Created;

    /// <summary>
    /// The direction to order in.
    /// </summary>
    [DefaultValue(SortDirection.Descending)]
    public SortDirection Direction { get; init; } = SortDirection.Descending;
}

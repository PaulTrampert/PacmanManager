namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The properties a repository listing may be ordered by.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Created"/> is first because <see cref="SortOptions{TSortFields}"/> takes the first
/// member as its default, so reordering this enum changes what an unsorted request returns.
/// </para>
/// <para>
/// Each member also declares which way round it runs when the caller omits <c>direction</c>: the
/// dates default to newest first, and <see cref="Name"/> to A→Z. See
/// <see cref="DefaultSortDirectionAttribute"/>.
/// </para>
/// </remarks>
public enum RepositorySortField
{
    /// <summary>When the repository was created.</summary>
    Created,

    /// <summary>When the repository was last modified.</summary>
    Updated,

    /// <summary>The repository name.</summary>
    [DefaultSortDirection(SortDirection.Ascending)]
    Name,
}

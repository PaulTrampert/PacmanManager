namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The properties a repository listing may be ordered by.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Name"/> is first because <see cref="SortOptions{TSortFields}"/> takes the first
/// member as its default, so reordering this enum changes what an unsorted request returns. A
/// listing nobody has ordered comes back by name, which is the order someone looking a repository
/// up reads it in.
/// </para>
/// <para>
/// Each member also declares which way round it runs when the caller omits <c>direction</c>: the
/// dates default to newest first, and <see cref="Name"/> to A→Z. See
/// <see cref="DefaultSortDirectionAttribute"/>.
/// </para>
/// </remarks>
public enum RepositorySortField
{
    /// <summary>The repository name.</summary>
    [DefaultSortDirection(SortDirection.Ascending)]
    Name,

    /// <summary>When the repository was created.</summary>
    Created,

    /// <summary>When the repository was last modified.</summary>
    Updated,
}

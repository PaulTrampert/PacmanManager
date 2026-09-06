namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The properties a repository listing may be ordered by.
/// </summary>
/// <remarks>
/// <see cref="Created"/> is first because <see cref="SortOptions{TSortFields}"/> takes the first
/// member as its default, so reordering this enum changes what an unsorted request returns.
/// </remarks>
public enum RepositorySortField
{
    /// <summary>When the repository was created.</summary>
    Created,

    /// <summary>When the repository was last modified.</summary>
    Updated,

    /// <summary>The repository name.</summary>
    Name,
}

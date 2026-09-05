namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The properties a repository listing may be ordered by.
/// </summary>
public enum RepositorySortField
{
    /// <summary>When the repository was created.</summary>
    Created,

    /// <summary>When the repository was last modified.</summary>
    Updated,

    /// <summary>The repository name.</summary>
    Name,
}

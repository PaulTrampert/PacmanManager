namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The orderings a repository listing may be requested in.
/// </summary>
public enum RepositorySortOrder
{
    /// <summary>Newest first. The default.</summary>
    CreatedDesc,

    /// <summary>Oldest first.</summary>
    CreatedAsc,

    /// <summary>Most recently updated first.</summary>
    UpdatedDesc,

    /// <summary>Least recently updated first.</summary>
    UpdatedAsc,

    /// <summary>Alphabetical by name.</summary>
    NameAsc,

    /// <summary>Reverse alphabetical by name.</summary>
    NameDesc,
}

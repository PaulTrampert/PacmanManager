namespace PacmanManager.RepoHost.Models;

/// <summary>
/// What the caller may do to a repository, computed for the caller of each request
/// and never stored.
/// </summary>
public record RepositoryPermissions
{
    /// <summary>
    /// Permits nothing: what an anonymous caller is given, and the value of a
    /// <see cref="Repository"/> nobody has computed permissions for.
    /// </summary>
    public static RepositoryPermissions None { get; } = new() { Update = false, Delete = false };

    /// <summary>
    /// Whether the caller may update the repository: rename it, change its visibility or change
    /// its architectures.
    /// </summary>
    public required bool Update { get; init; }

    /// <summary>
    /// Whether the caller may delete the repository.
    /// </summary>
    public required bool Delete { get; init; }
}

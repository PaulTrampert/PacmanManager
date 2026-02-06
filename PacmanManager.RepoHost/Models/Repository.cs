namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Represents a pacman repository that can be hosted and managed.
/// </summary>
public record Repository
{
    /// <summary>
    /// Repository name (e.g., "core", "extra", "custom-repo").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Repository architecture (e.g., "x86_64", "any").
    /// </summary>
    public string Architecture { get; init; } = "x86_64";
    
    /// <summary>
    /// When the repository was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
    
    /// <summary>
    /// When the repository was last modified.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
    
    /// <summary>
    /// Gets the file system path for this repository based on its name and architecture.
    /// </summary>
    /// <param name="baseRepositoryPath">The base path where repositories are stored.</param>
    /// <returns>The full path to this repository's directory.</returns>
    public string GetPath(string baseRepositoryPath)
    {
        return Path.Combine(baseRepositoryPath, Name, Architecture);
    }
}

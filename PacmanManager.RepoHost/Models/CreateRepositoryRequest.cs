namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Request model for creating a new repository.
/// </summary>
public record CreateRepositoryRequest
{
    /// <summary>
    /// Repository name (e.g., "core", "extra", "custom-repo").
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Human-readable description of the repository.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Repository architecture (e.g., "x86_64", "any"). Defaults to "x86_64".
    /// </summary>
    public string Architecture { get; init; } = "x86_64";
    
    /// <summary>
    /// Whether the repository is enabled/active. Defaults to true.
    /// </summary>
    public bool IsEnabled { get; init; } = true;
}

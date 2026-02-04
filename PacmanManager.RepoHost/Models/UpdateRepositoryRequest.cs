namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Request model for updating an existing repository.
/// </summary>
public record UpdateRepositoryRequest
{
    /// <summary>
    /// Repository name (e.g., "core", "extra", "custom-repo").
    /// </summary>
    public string? Name { get; init; }
    
    /// <summary>
    /// Human-readable description of the repository.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Repository architecture (e.g., "x86_64", "any").
    /// </summary>
    public string? Architecture { get; init; }
    
    /// <summary>
    /// Whether the repository is enabled/active.
    /// </summary>
    public bool? IsEnabled { get; init; }
}

using System.ComponentModel.DataAnnotations;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Validation;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Request model for creating a new repository.
/// </summary>
public record WriteRepositoryRequest
{
    /// <summary>
    /// Repository name (e.g., "core", "extra", "custom-repo").
    /// </summary>
    [Required]
    [MaxLength(255)]
    [MinLength(1)]
    [RegularExpression(RegularExpressions.RepositoryName)]
    public required string Name { get; init; }
    
    /// <summary>
    /// The machine architectures the repository publishes a database for. Defaults to every
    /// architecture a repository may support. Must be non-empty, and each element must be one a
    /// repository may support; <c>any</c> is never one, since it describes a package rather than a
    /// repository. A package built for <c>any</c> is published into every one of these
    /// architectures' databases.
    /// </summary>
    [Required]
    [NotEmpty]
    [SupportedArchitecture]
    public IEnumerable<string> SupportedArchitectures { get; init; } =
        PacmanRepositoryValidationConstants.DefaultArchitecture;

    /// <summary>
    /// Whether the repository is public.
    /// </summary>
    public bool IsPublic { get; init; } = false;
}

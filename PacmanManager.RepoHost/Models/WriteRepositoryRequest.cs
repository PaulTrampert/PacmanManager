using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
    /// Repository architecture (e.g., "x86_64", "any"). Defaults to "x86_64".
    /// </summary>
    [DefaultValue("x86_64")]
    [AllowedValues("x86_64", "any")]
    public string Architecture { get; init; } = "x86_64";

    /// <summary>
    /// Whether the repository is public.
    /// </summary>
    public bool IsPublic { get; init; } = false;
}

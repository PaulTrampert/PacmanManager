using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Validation;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Identifies a repository by its natural key rather than by its database id.
/// </summary>
/// <remarks>
/// A repository name on its own does not identify anything. Names are unique per owner and
/// architecture — the same uniqueness constraint the database enforces on
/// <see cref="PacmanRepository"/> — so two users may each own a repository called "custom", and
/// one user may own an <c>x86_64</c> and an <c>any</c> repository under that name. All three
/// parts are therefore required to resolve a name to a single repository.
/// </remarks>
public record RepositoryKey
{
    /// <summary>
    /// The id of the user who owns the repository.
    /// </summary>
    [Required]
    public required Guid OwnerId { get; init; }

    /// <summary>
    /// Repository name (e.g., "core", "extra", "custom-repo").
    /// </summary>
    [Required]
    [MaxLength(PacmanRepositoryValidationConstants.NameMaxLength)]
    [MinLength(PacmanRepositoryValidationConstants.NameMinLength)]
    [RegularExpression(RegularExpressions.RepositoryName)]
    public required string Name { get; init; }

    /// <summary>
    /// Repository architecture (e.g., "x86_64", "any"). Defaults to "x86_64".
    /// </summary>
    [DefaultValue("x86_64")]
    [AllowedValues("x86_64", "any")]
    public string Architecture { get; init; } = "x86_64";
}

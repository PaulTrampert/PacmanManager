using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace PacmanManager.Entities;

// A repository name belongs to exactly one repository across the whole deployment.
[Index(nameof(Name), IsUnique = true)]
// Serves the owner filter on the repository listing, which the unique index no longer starts with.
[Index(nameof(OwnerId))]
public record PacmanRepository
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    [Required]
    [MaxLength(PacmanRepositoryValidationConstants.NameMaxLength)]
    [MinLength(PacmanRepositoryValidationConstants.NameMinLength)]
    public required string Name { get; set; }
    
    [Required]
    public Guid OwnerId { get; set; }
    
    public virtual User Owner { get; set; }
    
    /// <summary>
    /// The machine architectures this repository publishes a database for. Never <c>any</c>, which
    /// describes a package rather than a repository; an <c>any</c> package is listed in every one of
    /// these architectures' databases.
    /// </summary>
    /// <remarks>
    /// The allowed values are <see cref="PacmanRepositoryValidationConstants.SupportedArchitectures"/>,
    /// which the wire model enforces. Stored as <c>text[]</c> under Npgsql.
    /// </remarks>
    [Required]
    public IEnumerable<string> SupportedArchitectures { get; set; } = [];

    public bool IsPublic { get; set; } = false;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
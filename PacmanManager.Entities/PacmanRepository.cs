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
    
    [Required]
    [MaxLength(PacmanRepositoryValidationConstants.ArchitectureMaxLength)]
    public required string Architecture { get; set; } = "x86_64";

    public bool IsPublic { get; set; } = false;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
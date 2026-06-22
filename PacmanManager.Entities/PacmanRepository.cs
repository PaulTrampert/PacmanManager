using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace PacmanManager.Entities;

[Index(nameof(OwnerId), nameof(Name), nameof(Architecture), IsUnique = true)]
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

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
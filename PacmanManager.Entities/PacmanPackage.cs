using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PacmanManager.Entities;

[Index(nameof(RepositoryId), nameof(Name), IsUnique = true)]
public record PacmanPackage
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();
    
    [MaxLength(PacmanPackageValidationConstants.NameMaxLength)]
    [RegularExpression(PacmanPackageValidationConstants.NameRegexPattern)]
    [Required]
    public required string Name { get; init; }
    
    [MaxLength(PacmanPackageValidationConstants.VersionMaxLength)]
    [RegularExpression(PacmanPackageValidationConstants.VersionRegexPattern)]
    [Required]
    public required string Version { get; init; }
    
    [MaxLength(PacmanPackageValidationConstants.ArchitectureMaxLength)]
    [Required]
    public required string Architecture { get; init; }
    
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    [ForeignKey(nameof(Repository))]
    [Required]
    public required Guid RepositoryId { get; init; }
    
    public virtual PacmanRepository Repository { get; init; } = null!;
};
using System.ComponentModel.DataAnnotations;

namespace PacmanManager.Entities;

public record PacmanRepository
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();

    [Required]
    [MaxLength(PacmanRepositoryValidationConstants.NameMaxLength)]
    [MinLength(PacmanRepositoryValidationConstants.NameMinLength)]
    public required string Name { get; init; }
    
    [Required]
    [MaxLength(PacmanRepositoryValidationConstants.ArchitectureMaxLength)]
    public string Architecture { get; init; } = "x86_64";
    
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    [Required]
    public required string FileSystemPath { get; init; }
}
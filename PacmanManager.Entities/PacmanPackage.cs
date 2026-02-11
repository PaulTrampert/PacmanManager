using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PacmanManager.Entities;

public record PacmanPackage
{
    [Key]
    public Guid Id { get; init; } = Guid.NewGuid();
    
    [MaxLength(255)]
    [RegularExpression(@"[a-z\d_@][a-z\d_-\+@\.]+")]
    [Required]
    public required string Name { get; init; }
    
    [MaxLength(255)]
    [RegularExpression(@"(\d+:)?[a-zA-Z\d_\.\+]+(-\d+)?")]
    [Required]
    public required string Version { get; init; }
    
    [MaxLength(255)]
    [Required]
    public required string Architecture { get; init; }
    
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    [ForeignKey(nameof(Repository))]
    [Required]
    public required Guid RepositoryId { get; init; }
    
    public virtual PacmanRepository Repository { get; init; } = null!;
};
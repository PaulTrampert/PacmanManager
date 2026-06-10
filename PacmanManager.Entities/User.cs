using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace PacmanManager.Entities;

[Index(nameof(Email), IsUnique = true)]
public record User
{
    [Key] 
    public Guid Id { get; init; } = Guid.CreateVersion7();
    
    [Required]
    [MaxLength(UserValidationConstants.DisplayNameMaxLength)]
    public required string DisplayName { get; set; }
    
    [MaxLength(UserValidationConstants.EmailMaxLength)]
    [RegularExpression(UserValidationConstants.EmailRegex)]
    public required string Email { get; set; }
};
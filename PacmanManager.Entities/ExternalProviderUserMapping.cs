using Microsoft.EntityFrameworkCore;

namespace PacmanManager.Entities;

[Index(nameof(ExternalAuthority), nameof(ExternalId), IsUnique = true)]
public record ExternalProviderUserMapping
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string ExternalId { get; set; }
    public required string ExternalAuthority { get; set; }
    public virtual User User { get; set; }
}
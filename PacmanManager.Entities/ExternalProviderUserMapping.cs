namespace PacmanManager.Entities;

public record ExternalProviderUserMapping
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string ExternalId { get; set; }
    public required string ExternalAuthority { get; set; }
    public virtual User User { get; set; }
}
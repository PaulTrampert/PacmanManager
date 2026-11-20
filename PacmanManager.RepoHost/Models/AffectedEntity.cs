using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// One entity a job acts on, as <c>GET /api/v1/Jobs/{id}</c> shows it.
/// </summary>
public record AffectedEntity
{
    /// <summary>
    /// The kind of entity <see cref="Id"/> names.
    /// </summary>
    public required JobEntityType Type { get; init; }

    /// <summary>
    /// The entity's id. It may name an entity that has since been deleted.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Maps a stored affected entity onto this model.
    /// </summary>
    /// <param name="entity">The stored row.</param>
    /// <returns>The model.</returns>
    public static AffectedEntity FromJobAffectedEntity(JobAffectedEntity entity) => new()
    {
        Type = entity.EntityType,
        Id = entity.EntityId,
    };
}

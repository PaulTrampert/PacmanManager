using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace PacmanManager.Entities;

/// <summary>
/// One entity a <see cref="Job"/> acts on, recorded as soon as it is known: whoever schedules the job
/// records what it already names, and the handler that runs it reports what it creates.
/// </summary>
/// <remarks>
/// <see cref="EntityId"/> is deliberately not a foreign key: a job outlives what it affected, and the
/// record that it affected an entity stays true after that entity is deleted.
/// </remarks>
[PrimaryKey(nameof(JobId), nameof(EntityType), nameof(EntityId))]
public record JobAffectedEntity
{
    /// <summary>
    /// Id of the job that affects the entity.
    /// </summary>
    [Required]
    public Guid JobId { get; init; }

    /// <summary>
    /// The job that affects the entity. Deleting the job deletes its affected entities.
    /// </summary>
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public virtual Job Job { get; set; } = null!;

    /// <summary>
    /// The kind of entity <see cref="EntityId"/> names.
    /// </summary>
    [Required]
    public JobEntityType EntityType { get; init; }

    /// <summary>
    /// The entity's id. Not a foreign key, so it survives the entity's deletion.
    /// </summary>
    [Required]
    public Guid EntityId { get; init; }
}

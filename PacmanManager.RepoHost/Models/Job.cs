using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// One unit of off-request work, such as a package publish, as its caller sees it.
/// </summary>
/// <remarks>
/// The wire model leaves out the job's message and who scheduled it: the caller is always the user
/// who scheduled it, and the message is the request they already sent.
/// </remarks>
public record Job
{
    /// <summary>
    /// The job's id, which names it in <c>GET /api/v1/Jobs/{id}</c>.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Where the job is in its life. <c>Completed</c> and <c>Failed</c> are final.
    /// </summary>
    public required JobState State { get; init; }

    /// <summary>
    /// When the job was scheduled.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The entities the job acts on, as far as they are known so far: a publish names its repository
    /// from the start and its package once it completes. Ordered by type, then id.
    /// </summary>
    public required IReadOnlyList<AffectedEntity> AffectedEntities { get; init; }

    /// <summary>
    /// Why the job failed. Present only when <see cref="State"/> is <c>Failed</c>.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Maps a stored job, loaded with its affected entities, onto this model.
    /// </summary>
    /// <param name="job">The stored job.</param>
    /// <returns>The model.</returns>
    public static Job FromJob(PacmanManager.Entities.Job job) => new()
    {
        Id = job.Id,
        State = job.State,
        CreatedAt = job.CreatedAt,
        AffectedEntities = job.AffectedEntities
            .OrderBy(e => e.EntityType)
            .ThenBy(e => e.EntityId)
            .Select(AffectedEntity.FromJobAffectedEntity)
            .ToList(),
        Error = job.State == JobState.Failed ? job.Error : null,
    };
}

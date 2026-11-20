using PacmanManager.Authentication;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Reads the jobs that record off-request work, as the current actor may see them.
/// </summary>
/// <remarks>
/// Actor-scoped, like every other service: what the actor supplied by <see cref="IActorAccessor"/>
/// may see is decided here, by <see cref="JobAccessPolicy"/>, not by the caller.
/// </remarks>
public interface IJobService
{
    /// <summary>
    /// Retrieves a job by id.
    /// </summary>
    /// <param name="jobId">The job's id.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// The job, or <c>null</c> if there is no job with that id the actor may see, which includes a
    /// job another user scheduled.
    /// </returns>
    /// <exception cref="NoCurrentUserException">Thrown when the actor has no identity.</exception>
    Task<Job?> GetJobAsync(Guid jobId, CancellationToken ct = default);
}

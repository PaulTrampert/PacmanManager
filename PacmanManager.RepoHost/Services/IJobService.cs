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

    /// <summary>
    /// Retrieves a page of the jobs the actor may see, which for a user are the jobs they scheduled.
    /// </summary>
    /// <param name="paginationParams">The pagination parameters.</param>
    /// <param name="filter">Caller-supplied criteria, which can only narrow the jobs the actor may see.</param>
    /// <param name="sort">The order to return results in. Defaults to newest first.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A page of jobs, each shown as <see cref="GetJobAsync"/> shows it.</returns>
    /// <exception cref="NoCurrentUserException">Thrown when the actor has no identity.</exception>
    Task<JobPaginatedResponse> GetJobsAsync(
        PaginationParams paginationParams,
        JobFilter? filter = null,
        SortOptions<JobSortField>? sort = null,
        CancellationToken ct = default);
}

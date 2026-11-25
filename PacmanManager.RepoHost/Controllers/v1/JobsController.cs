using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Controllers.v1;

/// <summary>
/// Controller for jobs, the records of work done off-request, such as a package publish.
/// </summary>
/// <remarks>
/// This controller performs no authorization of its own beyond requiring authentication.
/// <see cref="IJobService"/> decides which jobs the caller may see, so a job another user scheduled
/// is reported as missing, exactly like one that does not exist.
/// </remarks>
[ApiController]
[Route(ControllerConstants.ControllerBaseRoute)]
[Authorize]
public class JobsController(IJobService jobService, ILogger<JobsController> logger) : ControllerBase
{
    /// <summary>
    /// List the caller's own jobs, to find one they lost track of or see which have finished.
    /// </summary>
    /// <param name="paging">Where in the result set to start and how much to return.</param>
    /// <param name="filter">Criteria for narrowing the listing.</param>
    /// <param name="sort">The order to return results in. Defaults to newest first.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>A page of the caller's jobs, each shown as <c>GET /api/v1/Jobs/{id}</c> shows it.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(JobPaginatedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<JobPaginatedResponse>> Get(
        [FromQuery] PaginationParams paging,
        [FromQuery] JobFilter filter,
        [FromQuery] SortOptions<JobSortField> sort,
        CancellationToken ct = default)
    {
        logger.LogInformation("Listing jobs with filter {@Filter} sorted by {@Sort}", filter, sort);

        var result = await jobService.GetJobsAsync(paging, filter, sort, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a job by ID, to find out what happened to the work it records.
    /// </summary>
    /// <param name="jobId">The job's ID.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>The job.</returns>
    /// <remarks>
    /// <c>error</c> is present only when <c>state</c> is <c>Failed</c>. <c>affectedEntities</c> lists
    /// what is known so far. A job scheduled by another user is a <c>404</c>, exactly like one that
    /// does not exist.
    /// </remarks>
    [HttpGet("{jobId:guid}")]
    [ProducesResponseType(typeof(Job), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Job>> GetById(Guid jobId, CancellationToken ct = default)
    {
        logger.LogInformation("Getting job {JobId}", jobId);

        var job = await jobService.GetJobAsync(jobId, ct);
        return job is null ? NotFound() : Ok(job);
    }
}

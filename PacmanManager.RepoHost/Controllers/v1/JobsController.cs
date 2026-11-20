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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Controllers.v1;

/// <summary>
/// Controller for managing pacman repositories.
/// </summary>
/// <remarks>
/// This controller performs no authorization of its own beyond requiring authentication where an
/// operation needs an identity. Visibility and ownership are enforced by
/// <see cref="IRepositoryService"/>, so the same rules apply to callers that never go through
/// this controller. A repository the caller may not see is reported as missing.
/// </remarks>
[ApiController]
[Route(ControllerConstants.ControllerBaseRoute)]
public class RepositoriesController(IRepositoryService repositoryService, ILogger<RepositoriesController> logger) : ControllerBase
{
    /// <summary>
    /// List the repositories visible to the caller.
    /// </summary>
    /// <param name="paging">Where in the result set to start and how much to return.</param>
    /// <param name="filter">Criteria for narrowing the listing.</param>
    /// <param name="sort">The order to return results in.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>A page of repositories. Anonymous callers see public repositories only.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<Repository>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    public async Task<ActionResult<PaginatedResponse<Repository>>> Get(
        [FromQuery] PaginationParams paging,
        [FromQuery] RepositoryFilter filter,
        [FromQuery] SortOptions<RepositorySortField> sort,
        CancellationToken ct = default)
    {
        logger.LogInformation("Listing repositories with filter {@Filter} sorted by {@Sort}", filter, sort);

        var result = await repositoryService.GetRepositoriesAsync(paging, filter, sort, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a specific repository by ID.
    /// </summary>
    /// <param name="id">Repository ID.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>The requested repository.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Repository), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<ActionResult<Repository>> GetById(Guid id, CancellationToken ct = default)
    {
        logger.LogInformation("Getting repository {RepositoryId}", id);

        var repository = await repositoryService.GetRepositoryByIdAsync(id, ct);
        if (repository == null)
        {
            return NotFound();
        }

        return Ok(repository);
    }

    /// <summary>
    /// Create a new repository owned by the caller.
    /// </summary>
    /// <param name="request">Repository creation request.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>The created repository.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Repository), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize]
    public async Task<ActionResult<Repository>> Create([FromBody] WriteRepositoryRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("Creating repository {RepositoryName}", request.Name);

        var result = await repositoryService.CreateRepositoryAsync(request, ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Update an existing repository. Only its owner may do so.
    /// </summary>
    /// <param name="id">Repository ID.</param>
    /// <param name="request">Repository update request.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>The updated repository.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Repository), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize]
    public async Task<ActionResult<Repository>> Update(Guid id, [FromBody] WriteRepositoryRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("Updating repository {RepositoryId}", id);

        var result = await repositoryService.UpdateRepositoryAsync(id, request, ct);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Delete a repository. Only its owner may do so.
    /// </summary>
    /// <param name="id">Repository ID.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        logger.LogInformation("Deleting repository {RepositoryId}", id);

        return await repositoryService.DeleteRepositoryAsync(id, ct)
            ? NoContent()
            : NotFound();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Controllers.v1;

/// <summary>
/// Controller for managing pacman repositories.
/// </summary>
[ApiController]
[Route(ControllerConstants.ControllerBaseRoute)]
public class RepositoryController(IRepositoryService repositoryService, ILogger<RepositoryController> logger) : ControllerBase
{
    /// <summary>
    /// Get all repositories.
    /// </summary>
    /// <returns>List of all repositories.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Repository>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<Repository>>> Get([FromQuery] PaginationParams query, CancellationToken ct = default)
    {
        logger.LogInformation("Getting all repositories");
        
        var result = await repositoryService.GetRepositoriesAsync(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a specific repository by ID.
    /// </summary>
    /// <param name="id">Repository ID.</param>
    /// <returns>The requested repository.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Repository), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    /// Create a new repository.
    /// </summary>
    /// <param name="request">Repository creation request.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>The created repository.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Repository), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize]
    public async Task<ActionResult<Repository>> Create([FromBody] WriteRepositoryRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("Creating repository {RepositoryName}", request.Name);

        var result = await repositoryService.CreateRepositoryAsync(request, ct);
        
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Update an existing repository.
    /// </summary>
    /// <param name="id">Repository ID.</param>
    /// <param name="request">Repository update request.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>The updated repository.</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Repository), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
    /// Delete a repository.
    /// </summary>
    /// <param name="name">Repository name.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize]
    public ActionResult Delete(string name)
    {
        logger.LogInformation("Deleting repository {RepositoryName}", name);
        
        // TODO: Implement repository deletion
        return NotFound();
    }
}

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
    public ActionResult<IEnumerable<Repository>> GetAll()
    {
        logger.LogInformation("Getting all repositories");
        
        // TODO: Implement repository storage/retrieval
        return Ok(Enumerable.Empty<Repository>());
    }

    /// <summary>
    /// Get a specific repository by name.
    /// </summary>
    /// <param name="name">Repository name.</param>
    /// <returns>The requested repository.</returns>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(Repository), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Repository> GetById(string name)
    {
        logger.LogInformation("Getting repository {RepositoryName}", name);
        
        // TODO: Implement repository retrieval
        return NotFound();
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
    public async Task<ActionResult<Repository>> Create([FromBody] WriteRepositoryRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("Creating repository {RepositoryName}", request.Name);

        var result = await repositoryService.CreateRepositoryAsync(request, ct);
        
        return CreatedAtAction(nameof(GetById), new { name = result.Name }, result);
    }

    /// <summary>
    /// Update an existing repository.
    /// </summary>
    /// <param name="name">Repository name.</param>
    /// <param name="request">Repository update request.</param>
    /// <returns>The updated repository.</returns>
    [HttpPut("{name}")]
    [ProducesResponseType(typeof(Repository), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<Repository> Update(string name, [FromBody] WriteRepositoryRequest request)
    {
        logger.LogInformation("Updating repository {RepositoryName}", name);
        
        // TODO: Implement repository update
        return NotFound();
    }

    /// <summary>
    /// Delete a repository.
    /// </summary>
    /// <param name="name">Repository name.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult Delete(string name)
    {
        logger.LogInformation("Deleting repository {RepositoryName}", name);
        
        // TODO: Implement repository deletion
        return NotFound();
    }
}

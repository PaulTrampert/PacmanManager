using Microsoft.AspNetCore.Mvc;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Controllers;

/// <summary>
/// Controller for managing pacman repositories.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RepositoryController : ControllerBase
{
    private readonly ILogger<RepositoryController> _logger;

    public RepositoryController(ILogger<RepositoryController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get all repositories.
    /// </summary>
    /// <returns>List of all repositories.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Repository>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<Repository>> GetAll()
    {
        _logger.LogInformation("Getting all repositories");
        
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
        _logger.LogInformation("Getting repository {RepositoryName}", name);
        
        // TODO: Implement repository retrieval
        return NotFound();
    }

    /// <summary>
    /// Create a new repository.
    /// </summary>
    /// <param name="request">Repository creation request.</param>
    /// <returns>The created repository.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Repository), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<Repository> Create([FromBody] CreateRepositoryRequest request)
    {
        _logger.LogInformation("Creating repository {RepositoryName}", request.Name);
        
        // TODO: Implement repository creation
        var repository = new Repository
        {
            Name = request.Name,
            Architecture = request.Architecture,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        
        return CreatedAtAction(nameof(GetById), new { name = repository.Name }, repository);
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
    public ActionResult<Repository> Update(string name, [FromBody] UpdateRepositoryRequest request)
    {
        _logger.LogInformation("Updating repository {RepositoryName}", name);
        
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
        _logger.LogInformation("Deleting repository {RepositoryName}", name);
        
        // TODO: Implement repository deletion
        return NotFound();
    }
}

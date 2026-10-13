using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Controllers.v1;

/// <summary>
/// Controller for the machine architectures a repository may support.
/// </summary>
/// <remarks>
/// The list is small, static and defined by the server, so it is a plain array rather than a
/// <see cref="PaginatedResponse{T, TSortField}"/>, and takes no paging, filter or sort parameters.
/// </remarks>
[ApiController]
[Route(ControllerConstants.ControllerBaseRoute)]
public class ArchitecturesController(ILogger<ArchitecturesController> logger) : ControllerBase
{
    /// <summary>
    /// List the architectures a repository may support.
    /// </summary>
    /// <returns>Every supported architecture, with its display label.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Architecture>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    public ActionResult<IEnumerable<Architecture>> Get()
    {
        logger.LogInformation("Listing supported architectures");

        return Ok(Architecture.Supported.ToList());
    }
}

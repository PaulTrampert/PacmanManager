using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Controllers.v1;

/// <summary>
/// Controller for reading the packages published into hosted repositories.
/// </summary>
/// <remarks>
/// <para>
/// This controller performs no authorization of its own. Packages have no visibility of their own —
/// a package is visible exactly when its repository is — and <see cref="IPackageService"/> enforces
/// that, so the same rules apply to callers that never go through this controller.
/// </para>
/// <para>
/// A package the caller may not see is reported as missing. The resulting <c>404</c> covers three
/// cases that have to be indistinguishable from outside: the package does not exist, the repository
/// does not exist, and the repository is private and belongs to someone else.
/// </para>
/// <para>
/// The routes under <see cref="ControllerConstants.RepositoryScopedPackagesRoute"/> are aliases.
/// They resolve <c>(repositoryId, name)</c> to the same package the id form returns and go through
/// the same service, because the natural key is what a build script actually knows: it has just
/// built a package for a repository and has never seen a package id.
/// </para>
/// </remarks>
[ApiController]
[Route(ControllerConstants.ControllerBaseRoute)]
public class PackagesController(
    IPackageService packageService,
    IRepositoryService repositoryService,
    ILogger<PackagesController> logger) : ControllerBase
{
    /// <summary>
    /// The query string parameter the repository-scoped listing rejects, because its path segment
    /// already says which repository the listing covers.
    /// </summary>
    private const string RepositoryIdsParameter = nameof(PackageFilter.RepositoryIds);

    /// <summary>
    /// List the packages visible to the caller, across every repository they can see.
    /// </summary>
    /// <param name="paging">Where in the result set to start and how much to return.</param>
    /// <param name="filter">Criteria for narrowing the listing.</param>
    /// <param name="sort">The order to return results in.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>
    /// A page of packages. Anonymous callers see the packages of public repositories only.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<Package>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    public async Task<ActionResult<PaginatedResponse<Package>>> Get(
        [FromQuery] PaginationParams paging,
        [FromQuery] PackageFilter filter,
        [FromQuery] SortOptions<PackageSortField> sort,
        CancellationToken ct = default)
    {
        logger.LogInformation("Listing packages with filter {@Filter} sorted by {@Sort}", filter, sort);

        var result = await packageService.GetPackagesAsync(paging, filter, sort, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a specific package by ID.
    /// </summary>
    /// <param name="packageId">Package ID.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>The requested package.</returns>
    [HttpGet("{packageId:guid}")]
    [ProducesResponseType(typeof(Package), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<ActionResult<Package>> GetById(Guid packageId, CancellationToken ct = default)
    {
        logger.LogInformation("Getting package {PackageId}", packageId);

        var package = await packageService.GetPackageByIdAsync(packageId, ct);
        if (package == null)
        {
            return NotFound();
        }

        return Ok(package);
    }

    /// <summary>
    /// List the packages in a single repository.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="paging">Where in the result set to start and how much to return.</param>
    /// <param name="filter">
    /// Criteria for narrowing the listing. <see cref="PackageFilter.RepositoryIds"/> is not accepted
    /// here; the path segment supplies it.
    /// </param>
    /// <param name="sort">The order to return results in.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>A page of the repository's packages.</returns>
    /// <remarks>
    /// A caller who sends <c>repositoryIds</c> anyway gets a <c>400</c> rather than having it
    /// silently ignored, because the two would disagree whenever they named different repositories
    /// and a silently ignored criterion is the sort of thing a build script never notices.
    /// </remarks>
    [HttpGet(ControllerConstants.RepositoryScopedPackagesRoute)]
    [ProducesResponseType(typeof(PaginatedResponse<Package>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<ActionResult<PaginatedResponse<Package>>> GetForRepository(
        Guid repositoryId,
        [FromQuery] PaginationParams paging,
        [FromQuery] PackageFilter filter,
        [FromQuery] SortOptions<PackageSortField> sort,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Listing packages in repository {RepositoryId} with filter {@Filter} sorted by {@Sort}",
            repositoryId, filter, sort);

        // The binder maps an empty value to null, so the filter alone cannot tell "not supplied"
        // from "supplied with nothing in it". The raw query string can.
        if (Request.Query.Keys.Any(key => string.Equals(key, RepositoryIdsParameter, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(
                RepositoryIdsParameter,
                "This listing is already scoped to one repository by its path, so it does not accept a repository filter.");
            return ValidationProblem(ModelState);
        }

        // A repository the caller cannot see is reported as missing, so an empty page never doubles
        // as evidence that a private repository exists.
        if (await repositoryService.GetRepositoryByIdAsync(repositoryId, ct) == null)
        {
            return NotFound();
        }

        var result = await packageService.GetPackagesAsync(
            paging,
            filter with { RepositoryIds = [repositoryId] },
            sort,
            ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a package by its natural key, the repository holding it and its name.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="name">Package name.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>The requested package.</returns>
    /// <remarks>
    /// A repository holds exactly one version of a package, so this pair matches at most one
    /// package.
    /// </remarks>
    [HttpGet($"{ControllerConstants.RepositoryScopedPackagesRoute}/{{name}}")]
    [ProducesResponseType(typeof(Package), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<ActionResult<Package>> GetByName(Guid repositoryId, string name, CancellationToken ct = default)
    {
        logger.LogInformation("Getting package {PackageName} in repository {RepositoryId}", name, repositoryId);

        var package = await packageService.GetPackageByNameAsync(repositoryId, name, ct);
        if (package == null)
        {
            return NotFound();
        }

        return Ok(package);
    }
}

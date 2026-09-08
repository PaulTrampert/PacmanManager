using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PacmanManager.RepoHost.Config;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Controllers.v1;

/// <summary>
/// Controller for the packages held by hosted repositories.
/// </summary>
/// <remarks>
/// <para>
/// This controller performs no authorization of its own beyond requiring authentication where an
/// operation needs an identity. Packages have no visibility of their own — a package is visible
/// exactly when its repository is — and <see cref="IPackageService"/> enforces that, along with the
/// publish permission, so the same rules apply to callers that never go through this controller.
/// </para>
/// <para>
/// A package the caller may not see is reported as missing. The resulting <c>404</c> covers three
/// cases that have to be indistinguishable from outside: the package does not exist, the repository
/// does not exist, and the repository is private and belongs to someone else.
/// </para>
/// <para>
/// The read routes under <see cref="ControllerConstants.RepositoryScopedPackagesRoute"/> are
/// aliases. They resolve <c>(repositoryId, name)</c> to the same package the id form returns and go
/// through the same service, because the natural key is what a build script actually knows: it has
/// just built a package for a repository and has never seen a package id. Publishing has no id-keyed
/// form at all, since the id is what publishing produces.
/// </para>
/// </remarks>
[ApiController]
[Route(ControllerConstants.ControllerBaseRoute)]
public class PackagesController(
    IPackageService packageService,
    IRepositoryService repositoryService,
    IOptions<PackagePublishingConfig> publishingConfig,
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

    /// <summary>
    /// Download a package's file by the package's ID.
    /// </summary>
    /// <param name="packageId">Package ID.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>The stored package file, as <c>application/octet-stream</c>.</returns>
    /// <remarks>
    /// <para>
    /// The response carries a <c>Content-Disposition</c> naming the file as it is stored —
    /// <c>{name}-{version}-{architecture}.pkg.tar.{ext}</c>, the basename <c>repo-add</c> recorded
    /// — so a client that saves it to disk ends up with the name pacman expects rather than a
    /// package id. The body is streamed from disk and never buffered, since a package file can run
    /// to hundreds of megabytes.
    /// </para>
    /// <para>
    /// This downloads one package by identity. It is not a pacman mirror: a client configured with
    /// <c>Server = …</c> needs the repository's <c>.db.tar.gz</c> and every package file resolvable
    /// under one base URL by basename, which is separate work.
    /// </para>
    /// </remarks>
    [HttpGet("{packageId:guid}/content")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<IActionResult> GetContentById(Guid packageId, CancellationToken ct = default)
    {
        logger.LogInformation("Downloading package {PackageId}", packageId);

        var content = await packageService.GetPackageContentByIdAsync(packageId, ct);
        if (content == null)
        {
            return NotFound();
        }

        return File(content.Content, MediaTypeNames.Application.Octet, content.FileName);
    }

    /// <summary>
    /// Download a package's file by its natural key, the repository holding it and its name.
    /// </summary>
    /// <param name="repositoryId">Repository ID.</param>
    /// <param name="name">Package name.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>The stored package file, as <c>application/octet-stream</c>.</returns>
    /// <remarks>
    /// The alias of <see cref="GetContentById"/> for a caller that knows what it published rather
    /// than what the package was assigned. A repository holds exactly one version of a package, so
    /// this pair resolves to the same file.
    /// </remarks>
    [HttpGet($"{ControllerConstants.RepositoryScopedPackagesRoute}/{{name}}/content")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<IActionResult> GetContentByName(
        Guid repositoryId,
        string name,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "Downloading package {PackageName} in repository {RepositoryId}", name, repositoryId);

        var content = await packageService.GetPackageContentByNameAsync(repositoryId, name, ct);
        if (content == null)
        {
            return NotFound();
        }

        return File(content.Content, MediaTypeNames.Application.Octet, content.FileName);
    }

    /// <summary>
    /// Publish a package file into a repository, creating it or replacing the version already
    /// there.
    /// </summary>
    /// <param name="repositoryId">The repository to publish into.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>The stored package: <c>201</c> when it is new, <c>200</c> when it replaced one.</returns>
    /// <remarks>
    /// <para>
    /// The request body is the raw package file, as <c>application/octet-stream</c>. There is
    /// deliberately no <c>multipart/form-data</c> envelope: the name the file is stored under is
    /// derived from the metadata libalpm reads out of it, so a form would carry nothing worth
    /// trusting, and a raw body is markedly simpler to stream without buffering.
    /// </para>
    /// <para>
    /// Package files run to hundreds of megabytes, so the request's own body limit is raised from
    /// Kestrel's 30 MB default to the configured ceiling before the body is touched. Over that, the
    /// upload is a <c>413</c>.
    /// </para>
    /// </remarks>
    [HttpPost(ControllerConstants.RepositoryScopedPackagesRoute)]
    [ProducesResponseType(typeof(Package), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Package), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [Authorize]
    public async Task<ActionResult<Package>> Publish(Guid repositoryId, CancellationToken ct = default)
    {
        var maxUploadBytes = publishingConfig.Value.MaxUploadBytes;

        // Raised per request rather than globally, so that only the one route that streams a
        // package to disk is allowed to send this much. The feature is read-only once the body has
        // been read from, which is why this comes before the service call.
        if (HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = maxUploadBytes;
        }

        // A declared length over the ceiling is refused before anything is uploaded. A caller that
        // declares nothing, or lies, still runs into Kestrel's limit mid-body and gets the same
        // status from PackagePublishExceptionHandler.
        if (Request.ContentLength > maxUploadBytes)
        {
            logger.LogInformation(
                "Refusing a {ContentLength} byte package for repository {RepositoryId}; the limit is {Limit}",
                Request.ContentLength, repositoryId, maxUploadBytes);

            return Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "The uploaded package is too large.",
                detail: $"A package file may be at most {maxUploadBytes} bytes.");
        }

        logger.LogInformation("Publishing a package to repository {RepositoryId}", repositoryId);

        var result = await packageService.PublishPackageAsync(repositoryId, Request.Body, ct);
        if (result is null)
        {
            return NotFound();
        }

        if (!result.Created)
        {
            return Ok(result.Package);
        }

        // Built from the request path rather than from a route name, so it stays correct for
        // whichever API version the caller asked for and does not depend on the read routes.
        var location = $"{Request.Path.Value?.TrimEnd('/')}/{Uri.EscapeDataString(result.Package.Name)}";
        return Created(location, result.Package);
    }
}

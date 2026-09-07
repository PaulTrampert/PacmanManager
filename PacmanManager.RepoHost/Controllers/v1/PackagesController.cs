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
/// This controller performs no authorization of its own beyond requiring authentication where an
/// operation needs an identity. Visibility and the publish permission are enforced by
/// <see cref="IPackageService"/>, so the same rules apply to callers that never go through this
/// controller. A repository the caller may not see is reported as missing.
/// </remarks>
[ApiController]
[Route(ControllerConstants.ControllerBaseRoute)]
public class PackagesController(
    IPackageService packageService,
    IOptions<PackagePublishingConfig> publishingConfig,
    ILogger<PackagesController> logger) : ControllerBase
{
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
    [HttpPost("/api/v{version:apiVersion}/repositories/{repositoryId:guid}/packages")]
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

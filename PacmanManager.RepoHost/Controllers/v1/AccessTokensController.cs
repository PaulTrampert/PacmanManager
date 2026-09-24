using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Controllers.v1;

/// <summary>
/// Controller for the calling user's own access tokens, the credentials a <c>pacman</c> client
/// presents as HTTP Basic.
/// </summary>
/// <remarks>
/// <para>
/// This controller performs no authorization of its own beyond requiring authentication.
/// <see cref="IAccessTokenService"/> restricts every method to the actor's own tokens, so another
/// user's token is reported as missing.
/// </para>
/// <para>
/// A token's secret is returned once, in the <c>201</c> from <see cref="Create"/>, and never again:
/// only its hash is kept, and the listing model has no field to carry it.
/// </para>
/// </remarks>
[ApiController]
[Route(ControllerConstants.CurrentUserAccessTokensRoute)]
[Authorize]
public class AccessTokensController(IAccessTokenService accessTokenService, ILogger<AccessTokensController> logger) : ControllerBase
{
    /// <summary>
    /// List the caller's own access tokens. No secret is ever returned.
    /// </summary>
    /// <param name="paging">Where in the result set to start and how much to return.</param>
    /// <param name="filter">Criteria for narrowing the listing.</param>
    /// <param name="sort">The order to return results in. Defaults to newest first.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>A page of the caller's tokens.</returns>
    /// <remarks>
    /// <c>lastUsedAt</c> is deliberately coarse. It is written only when the stored value is older
    /// than a configured resolution, one hour by default, so it may lag a token's most recent use by
    /// up to that much.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<AccessToken>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PaginatedResponse<AccessToken>>> Get(
        [FromQuery] PaginationParams paging,
        [FromQuery] AccessTokenFilter filter,
        [FromQuery] SortOptions<AccessTokenSortField> sort,
        CancellationToken ct = default)
    {
        logger.LogInformation("Listing access tokens with filter {@Filter} sorted by {@Sort}", filter, sort);

        var result = await accessTokenService.GetAccessTokensAsync(paging, filter, sort, ct);
        return Ok(result);
    }

    /// <summary>
    /// Mint an access token for the caller.
    /// </summary>
    /// <param name="request">The token's name and optional expiry.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>
    /// The minted token, carrying its secret. <b>This is the only time the secret is returned</b>; it
    /// cannot be retrieved again.
    /// </returns>
    /// <remarks>
    /// An omitted or <c>null</c> <c>expiresAt</c> means the token never expires. A name the caller
    /// already has, compared without regard to case, is a <c>409</c>.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(CreatedAccessToken), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedAccessToken>> Create(
        [FromBody] CreateAccessTokenRequest request,
        CancellationToken ct = default)
    {
        logger.LogInformation("Minting access token {TokenName}", request.Name);

        var result = await accessTokenService.CreateAccessTokenAsync(request, ct);

        // No Location: there is no route that reads a single token back.
        return Created((string?)null, result);
    }

    /// <summary>
    /// Revoke one of the caller's own access tokens by deleting it.
    /// </summary>
    /// <param name="tokenId">The token's ID.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns><c>204</c> when the token is gone.</returns>
    /// <remarks>
    /// A token that belongs to another user is a <c>404</c>, exactly like one that does not exist.
    /// </remarks>
    [HttpDelete("{tokenId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid tokenId, CancellationToken ct = default)
    {
        logger.LogInformation("Revoking access token {TokenId}", tokenId);

        return await accessTokenService.DeleteAccessTokenAsync(tokenId, ct) ? NoContent() : NotFound();
    }
}

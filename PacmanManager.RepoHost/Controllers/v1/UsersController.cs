using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;
using PTrampert.SimplePatch;

namespace PacmanManager.RepoHost.Controllers.v1;

/// <summary>
/// Controller for reading users, and for changing the caller's own record.
/// </summary>
/// <remarks>
/// Every user is readable by everyone, so the listing and the lookup by id are anonymous and return
/// <see cref="PublicUserInfo"/>, which has no field capable of carrying an email. Only <c>me</c>
/// returns <see cref="CurrentUser"/>, and it addresses the caller and nobody else. The literal
/// <c>me</c> segment wins over the <c>{userId:guid}</c> constraint in route matching, so the two
/// routes do not conflict.
/// <para>
/// The only write routes are <c>PATCH me</c> and <c>POST me</c>. There is deliberately no write route by
/// id, at any id, so no route exists by which one user could change another.
/// </para>
/// </remarks>
[ApiController]
[Route(ControllerConstants.ControllerBaseRoute)]
public class UsersController(IUserManagementService userManagementService, ILogger<UsersController> logger) : ControllerBase
{
    /// <summary>
    /// List users.
    /// </summary>
    /// <param name="paging">Where in the result set to start and how much to return.</param>
    /// <param name="filter">Criteria for narrowing the listing.</param>
    /// <param name="sort">The order to return results in. Defaults to alphabetical by display name.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>A page of users, without their emails.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<PublicUserInfo>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    public async Task<ActionResult<PaginatedResponse<PublicUserInfo>>> Get(
        [FromQuery] PaginationParams paging,
        [FromQuery] UserFilter filter,
        [FromQuery] SortOptions<UserSortField> sort,
        CancellationToken ct = default)
    {
        logger.LogInformation("Listing users with filter {@Filter} sorted by {@Sort}", filter, sort);

        var result = await userManagementService.ListUsersAsync(paging, filter, sort, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a specific user by ID.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>The requested user, without their email.</returns>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(PublicUserInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<ActionResult<PublicUserInfo>> GetById(Guid userId, CancellationToken ct = default)
    {
        logger.LogInformation("Getting user {UserId}", userId);

        var user = await userManagementService.GetUserByIdAsync(userId, ct);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    /// <summary>
    /// Get the caller's own user record, including their email.
    /// </summary>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>The authenticated user.</returns>
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize]
    public async Task<ActionResult<CurrentUser>> GetCurrentUser(CancellationToken ct = default)
    {
        logger.LogInformation("Getting the current user");

        var user = await userManagementService.GetCurrentUserAsync(ct);
        return Ok(user);
    }

    /// <summary>
    /// Change the caller's own user record. Only the properties named in the body are changed; an
    /// omitted property keeps its present value.
    /// </summary>
    /// <param name="patch">
    /// The properties to change. An empty body changes nothing, and an explicit <c>null</c> display
    /// name is rejected rather than clearing the name.
    /// </param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>The updated user.</returns>
    [HttpPatch("me")]
    [ProducesResponseType(typeof(CurrentUser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize]
    public async Task<ActionResult<CurrentUser>> PatchCurrentUser(
        [FromBody] IPatchObject<WriteUserRequest> patch,
        CancellationToken ct = default)
    {
        logger.LogInformation("Updating the current user");

        // Patched onto a write model of the present values, never onto the entity: Patch returns a
        // new instance, and the service receives a complete, validated model.
        var current = await userManagementService.GetCurrentUserAsync(ct);
        var request = patch.Patch(WriteUserRequest.FromCurrentUser(current));

        var updated = await userManagementService.UpdateCurrentUserAsync(request, ct);
        return Ok(updated);
    }

    /// <summary>
    /// Replace the writable surface of the caller's own user record. Every property is required; use
    /// <c>PATCH me</c> to change only some of them.
    /// </summary>
    /// <param name="request">The complete writable surface.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>The updated user.</returns>
    [HttpPost("me")]
    [ProducesResponseType(typeof(CurrentUser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize]
    public async Task<ActionResult<CurrentUser>> PostCurrentUser(
        [FromBody] WriteUserRequest request,
        CancellationToken ct = default)
    {
        logger.LogInformation("Replacing the current user");

        var updated = await userManagementService.UpdateCurrentUserAsync(request, ct);
        return Ok(updated);
    }
}

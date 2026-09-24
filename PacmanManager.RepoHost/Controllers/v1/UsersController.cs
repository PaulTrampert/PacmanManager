using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Controllers.v1;

/// <summary>
/// Controller for reading users.
/// </summary>
/// <remarks>
/// Every user is readable by everyone, so the listing and the lookup by id are anonymous and return
/// <see cref="PublicUserInfo"/>, which has no field capable of carrying an email. Only <c>me</c>
/// returns <see cref="CurrentUser"/>, and it addresses the caller and nobody else. The literal
/// <c>me</c> segment wins over the <c>{userId:guid}</c> constraint in route matching, so the two
/// routes do not conflict.
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
}

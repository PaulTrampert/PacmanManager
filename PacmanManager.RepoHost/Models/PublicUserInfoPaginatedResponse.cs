namespace PacmanManager.RepoHost.Models;

/// <summary>
/// One page of the users listing.
/// </summary>
/// <remarks>
/// Named explicitly, rather than left as the generic <see cref="PaginatedResponse{T, TSortField}"/>, so
/// that its OpenAPI schema keeps the name <c>PublicUserInfoPaginatedResponse</c> the web app imports.
/// </remarks>
public record PublicUserInfoPaginatedResponse : PaginatedResponse<PublicUserInfo, UserSortField>;

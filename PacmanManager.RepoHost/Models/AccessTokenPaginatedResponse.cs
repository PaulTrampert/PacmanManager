namespace PacmanManager.RepoHost.Models;

/// <summary>
/// One page of the access tokens listing.
/// </summary>
/// <remarks>
/// Named explicitly, rather than left as the generic <see cref="PaginatedResponse{T, TSortField}"/>, so
/// that its OpenAPI schema keeps the name <c>AccessTokenPaginatedResponse</c> the web app imports.
/// </remarks>
public record AccessTokenPaginatedResponse : PaginatedResponse<AccessToken, AccessTokenSortField>;

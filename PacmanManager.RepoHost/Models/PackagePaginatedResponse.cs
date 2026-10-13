namespace PacmanManager.RepoHost.Models;

/// <summary>
/// One page of a packages listing.
/// </summary>
/// <remarks>
/// Named explicitly, rather than left as the generic <see cref="PaginatedResponse{T, TSortField}"/>, so
/// that its OpenAPI schema keeps the name <c>PackagePaginatedResponse</c> the web app imports.
/// </remarks>
public record PackagePaginatedResponse : PaginatedResponse<Package, PackageSortField>;

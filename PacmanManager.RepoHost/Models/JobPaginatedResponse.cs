namespace PacmanManager.RepoHost.Models;

/// <summary>
/// One page of the jobs listing.
/// </summary>
/// <remarks>
/// Named explicitly, rather than left as the generic <see cref="PaginatedResponse{T, TSortField}"/>, so
/// that its OpenAPI schema has the stable name <c>JobPaginatedResponse</c> for the web app to import.
/// </remarks>
public record JobPaginatedResponse : PaginatedResponse<Job, JobSortField>;

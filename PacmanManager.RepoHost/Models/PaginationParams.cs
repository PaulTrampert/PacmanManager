using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Where in a result set to start and how much of it to return. Criteria for narrowing the result
/// set itself live in a separate filter type, such as <see cref="RepositoryFilter"/>.
/// </summary>
public record PaginationParams
{
    /// <summary>
    /// The number of results to skip.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int Offset { get; init; }

    /// <summary>
    /// The maximum number of results to return.
    /// </summary>
    [DefaultValue(50)]
    [Range(1, 500)]
    public int PageSize { get; init; } = 50;
}

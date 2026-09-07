using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Translates a repository <see cref="SortOptions{TSortFields}"/> into query operators.
/// </summary>
internal static class RepositorySortExtensions
{
    /// <summary>
    /// Orders <paramref name="query"/> as requested, breaking ties by id so that paging is stable.
    /// </summary>
    /// <param name="query">The query to order.</param>
    /// <param name="sort">
    /// The requested ordering. A direction the caller omitted is resolved from the sort field, so
    /// <see cref="RepositorySortField.Name"/> runs A→Z and the dates newest first.
    /// </param>
    /// <returns>The ordered query.</returns>
    public static IOrderedQueryable<PacmanRepository> ApplySort(
        this IQueryable<PacmanRepository> query,
        SortOptions<RepositorySortField> sort)
    {
        var direction = sort.ResolveDirection();

        // The switch answers only which property the sort field names; applying the direction is the
        // same operation whichever property that is, so OrderByDirection does it once. It stays
        // generic in the key type, because erasing the types to a common object would box the key
        // and stop the ordering translating to SQL.
        var ordered = sort.SortBy switch
        {
            RepositorySortField.Name => query.OrderByDirection(r => r.Name, direction),
            RepositorySortField.Updated => query.OrderByDirection(r => r.UpdatedAt, direction),
            _ => query.OrderByDirection(r => r.CreatedAt, direction),
        };

        return ordered.ThenBy(r => r.Id);
    }
}

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

        // Matched on the pair rather than composed, because the key selectors have different
        // types and erasing that to object would stop the ordering translating to SQL.
        var ordered = (sort.SortBy, direction) switch
        {
            (RepositorySortField.Name, SortDirection.Ascending) => query.OrderBy(r => r.Name),
            (RepositorySortField.Name, _) => query.OrderByDescending(r => r.Name),
            (RepositorySortField.Updated, SortDirection.Ascending) => query.OrderBy(r => r.UpdatedAt),
            (RepositorySortField.Updated, _) => query.OrderByDescending(r => r.UpdatedAt),
            (_, SortDirection.Ascending) => query.OrderBy(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt),
        };

        return ordered.ThenBy(r => r.Id);
    }
}

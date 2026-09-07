using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Translates a package <see cref="SortOptions{TSortFields}"/> into query operators.
/// </summary>
internal static class PackageSortExtensions
{
    /// <summary>
    /// Orders <paramref name="query"/> as requested, breaking ties by id so that paging is stable.
    /// </summary>
    /// <param name="query">The query to order.</param>
    /// <param name="sort">
    /// The requested ordering. A direction the caller omitted is resolved from the sort field, so
    /// <see cref="PackageSortField.Name"/> runs A→Z and the dates and size biggest first.
    /// </param>
    /// <returns>The ordered query.</returns>
    public static IOrderedQueryable<PacmanPackage> ApplySort(
        this IQueryable<PacmanPackage> query,
        SortOptions<PackageSortField> sort)
    {
        var direction = sort.ResolveDirection();

        // Matched on the pair rather than composed, because the key selectors have different types
        // and erasing that to object would stop the ordering translating to SQL.
        var ordered = (sort.SortBy, direction) switch
        {
            (PackageSortField.Updated, SortDirection.Ascending) => query.OrderBy(p => p.UpdatedAt),
            (PackageSortField.Updated, _) => query.OrderByDescending(p => p.UpdatedAt),
            (PackageSortField.Created, SortDirection.Ascending) => query.OrderBy(p => p.CreatedAt),
            (PackageSortField.Created, _) => query.OrderByDescending(p => p.CreatedAt),
            (PackageSortField.InstalledSize, SortDirection.Ascending) => query.OrderBy(p => p.InstalledSize),
            (PackageSortField.InstalledSize, _) => query.OrderByDescending(p => p.InstalledSize),
            (_, SortDirection.Descending) => query.OrderByDescending(p => p.Name),
            _ => query.OrderBy(p => p.Name),
        };

        return ordered.ThenBy(p => p.Id);
    }
}

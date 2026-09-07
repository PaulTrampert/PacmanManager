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

        // The switch answers only which property the sort field names; applying the direction is the
        // same operation whichever property that is, so OrderByDirection does it once. It stays
        // generic in the key type, because erasing the types to a common object would box the key
        // and stop the ordering translating to SQL.
        var ordered = sort.SortBy switch
        {
            PackageSortField.Updated => query.OrderByDirection(p => p.UpdatedAt, direction),
            PackageSortField.Created => query.OrderByDirection(p => p.CreatedAt, direction),
            PackageSortField.InstalledSize => query.OrderByDirection(p => p.InstalledSize, direction),
            _ => query.OrderByDirection(p => p.Name, direction),
        };

        return ordered.ThenBy(p => p.Id);
    }
}

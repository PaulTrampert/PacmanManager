using System.Linq.Expressions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Translates a package <see cref="SortOptions{TSortFields}"/> into query operators.
/// </summary>
internal static class PackageSortExtensions
{
    /// <summary>
    /// The property each sort field names.
    /// </summary>
    private static readonly Dictionary<PackageSortField, Expression<Func<PacmanPackage, object>>> KeySelectors = new()
    {
        [PackageSortField.Name] = p => p.Name,
        [PackageSortField.Updated] = p => p.UpdatedAt,
        [PackageSortField.Created] = p => p.CreatedAt,
        [PackageSortField.InstalledSize] = p => p.InstalledSize,
    };

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
        SortOptions<PackageSortField> sort) =>
        query.ApplySort(sort, KeySelectors, p => p.Id);
}

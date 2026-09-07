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
    /// <remarks>
    /// Typed as <see cref="object"/> so that one table can hold selectors for properties of
    /// different types. That puts a <see cref="ExpressionType.Convert"/> node over each key, which
    /// EF Core strips when the conversion target is <see cref="object"/>, so the ordering still
    /// translates to SQL rather than being evaluated client side.
    /// </remarks>
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
        SortOptions<PackageSortField> sort)
    {
        // A sortBy bound from a query string can be a value the enum does not declare, which names
        // no property. Falling back to the default member means such a request is ordered the way an
        // unsorted one is, rather than throwing.
        var keySelector = KeySelectors.TryGetValue(sort.SortBy, out var selector)
            ? selector
            : KeySelectors[default];

        var ordered = sort.ResolveDirection() == SortDirection.Ascending
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);

        return ordered.ThenBy(p => p.Id);
    }
}

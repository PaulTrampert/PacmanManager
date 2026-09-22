using System.Linq.Expressions;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Translates a <see cref="SortOptions{TSortFields}"/> into query operators, for any listing.
/// </summary>
/// <remarks>
/// Every listing orders the same way — look the field up, resolve the direction, break ties by id —
/// and differs only in which property each sort field names. That table is the one thing a listing
/// supplies; see <see cref="RepositorySortExtensions"/> and <see cref="PackageSortExtensions"/>.
/// </remarks>
internal static class SortExtensions
{
    /// <summary>
    /// Orders <paramref name="query"/> as requested, breaking ties by id so that paging is stable.
    /// </summary>
    /// <typeparam name="TEntity">The type of the rows being ordered.</typeparam>
    /// <typeparam name="TSortField">The enum naming the properties the listing may be ordered by.</typeparam>
    /// <param name="query">The query to order.</param>
    /// <param name="sort">
    /// The requested ordering. A direction the caller omitted is resolved from the sort field by
    /// <see cref="SortOptions{TSortFields}.ResolveDirection"/>.
    /// </param>
    /// <param name="keySelectors">
    /// The property each sort field names. It must hold an entry for the enum's default member,
    /// which is what a field without an entry of its own falls back to.
    /// </param>
    /// <param name="idSelector">The row's unique id, which breaks ties between equal keys.</param>
    /// <returns>The ordered query.</returns>
    /// <remarks>
    /// The selectors are typed as <see cref="object"/> so that one table can hold selectors for
    /// properties of different types. That puts a <see cref="ExpressionType.Convert"/> node over each
    /// key, which EF Core strips when the conversion target is <see cref="object"/>, so the ordering
    /// still translates to SQL rather than being evaluated client side.
    /// </remarks>
    public static IOrderedQueryable<TEntity> ApplySort<TEntity, TSortField>(
        this IQueryable<TEntity> query,
        SortOptions<TSortField> sort,
        IReadOnlyDictionary<TSortField, Expression<Func<TEntity, object>>> keySelectors,
        Expression<Func<TEntity, object>> idSelector)
        where TSortField : struct, Enum
    {
        // A sortBy bound from a query string can be a value the enum does not declare, which names
        // no property. Falling back to the default member means such a request is ordered the way an
        // unsorted one is, rather than throwing.
        var keySelector = keySelectors.TryGetValue(sort.SortBy, out var selector)
            ? selector
            : keySelectors[default];

        var ordered = sort.ResolveDirection() == SortDirection.Ascending
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);

        return ordered.ThenBy(idSelector);
    }
}

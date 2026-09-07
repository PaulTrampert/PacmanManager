using System.Linq.Expressions;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// The ordering step every listing's <c>ApplySort</c> shares.
/// </summary>
/// <remarks>
/// Applying a <see cref="SortDirection"/> is the same operation for every listing and every sort
/// field, so it lives here once. That leaves each listing's <c>ApplySort</c> to answer only the
/// question that is actually its own — which property this sort field names — instead of
/// enumerating a case per (field, direction) pair.
/// </remarks>
internal static class SortExtensions
{
    /// <summary>
    /// Orders <paramref name="query"/> by <paramref name="keySelector"/>, the way round
    /// <paramref name="direction"/> asks for.
    /// </summary>
    /// <param name="query">The query to order.</param>
    /// <param name="keySelector">The property to order by.</param>
    /// <param name="direction">The direction to order in.</param>
    /// <typeparam name="T">The entity being ordered.</typeparam>
    /// <typeparam name="TKey">
    /// The type of the property being ordered by. Generic so that each sort field keeps its own key
    /// type: erasing them to a common <see cref="object"/> would box the key and stop the ordering
    /// translating to SQL.
    /// </typeparam>
    /// <returns>The ordered query.</returns>
    public static IOrderedQueryable<T> OrderByDirection<T, TKey>(
        this IQueryable<T> query,
        Expression<Func<T, TKey>> keySelector,
        SortDirection direction) =>
        direction == SortDirection.Ascending
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);
}

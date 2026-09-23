using System.Linq.Expressions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Translates an access token <see cref="SortOptions{TSortFields}"/> into query operators.
/// </summary>
internal static class AccessTokenSortExtensions
{
    /// <summary>
    /// The property each sort field names. <see cref="AccessTokenSortField.Name"/> orders by the
    /// normalized name, so that <c>laptop</c> and <c>Laptop2</c> sort together.
    /// </summary>
    private static readonly Dictionary<AccessTokenSortField, Expression<Func<PacmanAccessToken, object>>> KeySelectors = new()
    {
        [AccessTokenSortField.CreatedAt] = t => t.CreatedAt,
        [AccessTokenSortField.Name] = t => t.NormalizedName,
        [AccessTokenSortField.ExpiresAt] = t => t.ExpiresAt!,
        [AccessTokenSortField.LastUsedAt] = t => t.LastUsedAt!,
    };

    /// <summary>
    /// Orders <paramref name="query"/> as requested, breaking ties by id so that paging is stable.
    /// </summary>
    /// <param name="query">The query to order.</param>
    /// <param name="sort">The requested ordering.</param>
    /// <returns>The ordered query.</returns>
    public static IOrderedQueryable<PacmanAccessToken> ApplySort(
        this IQueryable<PacmanAccessToken> query,
        SortOptions<AccessTokenSortField> sort) =>
        query.ApplySort(sort, KeySelectors, t => t.Id);
}

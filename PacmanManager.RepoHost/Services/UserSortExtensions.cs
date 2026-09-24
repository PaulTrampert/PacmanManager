using System.Linq.Expressions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Translates a user <see cref="SortOptions{TSortFields}"/> into query operators.
/// </summary>
internal static class UserSortExtensions
{
    /// <summary>
    /// The property each sort field names. <see cref="UserSortField.DisplayName"/> orders by the
    /// normalized display name, so that <c>alex</c> and <c>Bea</c> sort alphabetically regardless of
    /// case.
    /// </summary>
    private static readonly Dictionary<UserSortField, Expression<Func<User, object>>> KeySelectors = new()
    {
        [UserSortField.DisplayName] = u => u.NormalizedDisplayName,
    };

    /// <summary>
    /// Orders <paramref name="query"/> as requested, breaking ties by id so that paging is stable.
    /// </summary>
    /// <param name="query">The query to order.</param>
    /// <param name="sort">The requested ordering.</param>
    /// <returns>The ordered query.</returns>
    public static IOrderedQueryable<User> ApplySort(
        this IQueryable<User> query,
        SortOptions<UserSortField> sort) =>
        query.ApplySort(sort, KeySelectors, u => u.Id);
}

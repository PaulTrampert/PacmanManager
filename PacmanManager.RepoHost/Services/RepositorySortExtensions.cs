using System.Linq.Expressions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Translates a repository <see cref="SortOptions{TSortFields}"/> into query operators.
/// </summary>
internal static class RepositorySortExtensions
{
    /// <summary>
    /// The property each sort field names.
    /// </summary>
    private static readonly Dictionary<RepositorySortField, Expression<Func<PacmanRepository, object>>> KeySelectors = new()
    {
        [RepositorySortField.Name] = r => r.Name,
        [RepositorySortField.Created] = r => r.CreatedAt,
        [RepositorySortField.Updated] = r => r.UpdatedAt,
    };

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
        SortOptions<RepositorySortField> sort) =>
        query.ApplySort(sort, KeySelectors, r => r.Id);
}

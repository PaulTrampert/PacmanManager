using System.Linq.Expressions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;
using Job = PacmanManager.Entities.Job;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Translates a job <see cref="SortOptions{TSortFields}"/> into query operators.
/// </summary>
internal static class JobSortExtensions
{
    /// <summary>
    /// The property each sort field names.
    /// </summary>
    private static readonly Dictionary<JobSortField, Expression<Func<Job, object>>> KeySelectors = new()
    {
        [JobSortField.CreatedAt] = j => j.CreatedAt,
    };

    /// <summary>
    /// Orders <paramref name="query"/> as requested, breaking ties by id so that paging is stable.
    /// </summary>
    /// <param name="query">The query to order.</param>
    /// <param name="sort">The requested ordering.</param>
    /// <returns>The ordered query.</returns>
    public static IOrderedQueryable<Job> ApplySort(
        this IQueryable<Job> query,
        SortOptions<JobSortField> sort) =>
        query.ApplySort(sort, KeySelectors, j => j.Id);
}

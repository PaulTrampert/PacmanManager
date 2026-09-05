using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Translates a <see cref="RepositoryFilter"/> into query operators.
/// </summary>
internal static class RepositoryQueryExtensions
{
    /// <summary>
    /// Narrows <paramref name="query"/> by the caller's criteria.
    /// </summary>
    /// <param name="query">The query to narrow. Must already be restricted to what the actor may see.</param>
    /// <param name="filter">The caller-supplied criteria.</param>
    /// <param name="actor">The actor, used to resolve criteria expressed relative to the caller.</param>
    /// <returns>The narrowed query.</returns>
    public static IQueryable<PacmanRepository> ApplyFilter(
        this IQueryable<PacmanRepository> query,
        RepositoryFilter filter,
        Actor actor)
    {
        if (!string.IsNullOrWhiteSpace(filter.NameContains))
        {
            var nameContains = filter.NameContains;
            query = query.Where(r => r.Name.Contains(nameContains));
        }

        if (!string.IsNullOrWhiteSpace(filter.Architecture))
        {
            var architecture = filter.Architecture;
            query = query.Where(r => r.Architecture == architecture);
        }

        if (filter.IsPublic is { } isPublic)
        {
            query = query.Where(r => r.IsPublic == isPublic);
        }

        if (filter.OwnerId is { } ownerId)
        {
            query = query.Where(r => r.OwnerId == ownerId);
        }

        if (filter.MineOnly)
        {
            // An actor with no user owns nothing, so the filter matches nothing rather than
            // degrading into "no owner restriction at all".
            query = actor.User is { } user
                ? query.Where(r => r.OwnerId == user.Id)
                : query.Where(_ => false);
        }

        return query;
    }

    /// <summary>
    /// Orders <paramref name="query"/> as requested, breaking ties by id so that paging is stable.
    /// </summary>
    /// <param name="query">The query to order.</param>
    /// <param name="sort">The requested ordering.</param>
    /// <returns>The ordered query.</returns>
    public static IOrderedQueryable<PacmanRepository> ApplySort(
        this IQueryable<PacmanRepository> query,
        RepositorySort sort)
    {
        var ordered = sort switch
        {
            RepositorySort.CreatedAsc => query.OrderBy(r => r.CreatedAt),
            RepositorySort.UpdatedDesc => query.OrderByDescending(r => r.UpdatedAt),
            RepositorySort.UpdatedAsc => query.OrderBy(r => r.UpdatedAt),
            RepositorySort.NameAsc => query.OrderBy(r => r.Name),
            RepositorySort.NameDesc => query.OrderByDescending(r => r.Name),
            _ => query.OrderByDescending(r => r.CreatedAt),
        };

        return ordered.ThenBy(r => r.Id);
    }
}

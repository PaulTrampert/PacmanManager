using System.Linq.Expressions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Models;
using PTrampert.QueryObjects;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// A <see cref="RepositoryFilter"/> bound to the actor whose listing it narrows.
/// </summary>
/// <remarks>
/// Every other criterion is self-contained and says so with a query attribute.
/// <see cref="RepositoryFilter.MineOnly"/> is not: it means "owned by whoever is asking", which
/// the filter on its own cannot know. Resolving it here rather than adding the caller's id to
/// <see cref="RepositoryFilter"/> keeps that id out of the bound query string, so no request can
/// nominate someone else as the caller. The expression built here is ANDed onto the ones the
/// attributes produce.
/// </remarks>
internal sealed record ActorScopedRepositoryFilter : RepositoryFilter, IQueryObject<PacmanRepository>
{
    private readonly Guid? _callerId;

    /// <summary>
    /// Binds <paramref name="filter"/> to <paramref name="actor"/>.
    /// </summary>
    /// <param name="filter">The caller-supplied criteria.</param>
    /// <param name="actor">The actor the criteria are expressed relative to.</param>
    public ActorScopedRepositoryFilter(RepositoryFilter filter, Actor actor) : base(filter)
    {
        _callerId = actor.User?.Id;
    }

    /// <inheritdoc />
    public Expression<Func<PacmanRepository, bool>>? BuildQueryExpression()
    {
        if (!MineOnly)
        {
            return null;
        }

        // An actor with no user owns nothing, so the criterion matches nothing rather than
        // degrading into "no owner restriction at all".
        if (_callerId is not { } callerId)
        {
            return _ => false;
        }

        return r => r.OwnerId == callerId;
    }
}

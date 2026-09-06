using System.Linq.Expressions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// The single definition of who may see and modify a repository.
/// </summary>
/// <remarks>
/// <para>
/// This type holds the rules so that <see cref="RepositoryService"/> can apply them at one
/// chokepoint per operation rather than restating them. It is intentionally free of any
/// database, HTTP or logging dependency, which makes the rules exhaustively unit-testable
/// on their own.
/// </para>
/// <para>The rules are:</para>
/// <list type="bullet">
///   <item><description>Anyone, including anonymous callers, may read a public repository.</description></item>
///   <item><description>A private repository is visible only to its owner. To everyone else it does not exist.</description></item>
///   <item><description>Only the owner may update or delete a repository.</description></item>
///   <item><description>Only an identified user may create a repository.</description></item>
/// </list>
/// </remarks>
internal sealed class RepositoryAccessPolicy
{
    /// <summary>
    /// Builds the predicate describing every repository <paramref name="actor"/> is allowed to see.
    /// </summary>
    /// <param name="actor">The actor to build visibility for.</param>
    /// <returns>An expression suitable for composition into an EF Core query.</returns>
    public Expression<Func<PacmanRepository, bool>> VisibleTo(Actor actor)
    {
        if (actor.IsSystem)
        {
            return _ => true;
        }

        if (actor.User is not { } user)
        {
            return r => r.IsPublic;
        }

        var userId = user.Id;
        return r => r.IsPublic || r.OwnerId == userId;
    }

    /// <summary>
    /// Decides whether <paramref name="actor"/> may modify or delete <paramref name="repository"/>.
    /// </summary>
    /// <param name="repository">The repository being modified.</param>
    /// <param name="actor">The actor attempting the modification.</param>
    /// <returns>The outcome of the check.</returns>
    /// <remarks>
    /// Callers are expected to have already restricted the repository to the actor's visible set,
    /// so a private repository reaching this method belongs to the actor. The
    /// <see cref="RepositoryAccess.NotFound"/> result is a second line of defence for callers
    /// that did not.
    /// </remarks>
    public RepositoryAccess CheckWrite(PacmanRepository repository, Actor actor)
    {
        if (actor.IsSystem)
        {
            return RepositoryAccess.Allowed;
        }

        if (actor.User is not { } user)
        {
            return RepositoryAccess.Unauthenticated;
        }

        if (repository.OwnerId == user.Id)
        {
            return RepositoryAccess.Allowed;
        }

        // A public repository is already known to exist, so admitting that and refusing the write
        // leaks nothing. A private one must keep pretending it is not there.
        return repository.IsPublic ? RepositoryAccess.Forbidden : RepositoryAccess.NotFound;
    }

    /// <summary>
    /// Decides whether <paramref name="actor"/> may create a repository.
    /// </summary>
    /// <param name="actor">The actor attempting the creation.</param>
    /// <returns>The outcome of the check.</returns>
    /// <remarks>
    /// Creation always needs a user, even for a system actor, because the new repository has to
    /// have an owner. A system host that needs to create repositories should use
    /// <see cref="Actor.SystemFor"/>.
    /// </remarks>
    public RepositoryAccess CheckCreate(Actor actor) =>
        actor.User is null ? RepositoryAccess.Unauthenticated : RepositoryAccess.Allowed;
}

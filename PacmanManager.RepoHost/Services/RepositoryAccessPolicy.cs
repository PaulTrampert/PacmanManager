using System.Linq.Expressions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using static PacmanManager.RepoHost.Authentication.ScopeValues;

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
/// <para>
/// Over all of them sits the actor's <see cref="Actor.Scope"/>, which only narrows. An actor whose
/// scope does not permit <c>repositories:read</c> sees what an anonymous caller sees, and a change is
/// <see cref="RepositoryAccess.Forbidden"/> unless the scope permits both that change and
/// <c>repositories:read</c>. The scope is asked after the no-user rule and before ownership.
/// </para>
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

        // A credential that may not read repositories reads them as a stranger would, so it can
        // never see less than a caller with no Authorization header at all.
        if (actor.User is not { } user || !actor.Scope.Permits(EntityNames.Repositories, ActionNames.Read))
        {
            return r => r.IsPublic;
        }

        var userId = user.Id;
        return r => r.IsPublic || r.OwnerId == userId;
    }

    /// <summary>
    /// Decides whether <paramref name="actor"/> may update <paramref name="repository"/>: rename it,
    /// change its visibility or change its architecture.
    /// </summary>
    /// <param name="repository">The repository being updated.</param>
    /// <param name="actor">The actor attempting the update.</param>
    /// <returns>The outcome of the check.</returns>
    /// <remarks>
    /// Callers are expected to have already restricted the repository to the actor's visible set,
    /// so a private repository reaching this method belongs to the actor. The
    /// <see cref="RepositoryAccess.NotFound"/> result is a second line of defence for callers
    /// that did not.
    /// </remarks>
    public RepositoryAccess CheckUpdate(PacmanRepository repository, Actor actor) =>
        CheckOwnership(repository, actor, ActionNames.Update);

    /// <summary>
    /// Decides whether <paramref name="actor"/> may delete <paramref name="repository"/>.
    /// </summary>
    /// <param name="repository">The repository being deleted.</param>
    /// <param name="actor">The actor attempting the deletion.</param>
    /// <returns>The outcome of the check.</returns>
    /// <remarks>
    /// <para>
    /// This is a verdict of its own rather than a second use of <see cref="CheckUpdate"/>, although
    /// the two agree today: a caller that may rename a repository need not be able to delete it.
    /// </para>
    /// <para>
    /// Callers are expected to have already restricted the repository to the actor's visible set,
    /// so a private repository reaching this method belongs to the actor. The
    /// <see cref="RepositoryAccess.NotFound"/> result is a second line of defence for callers
    /// that did not.
    /// </para>
    /// </remarks>
    public RepositoryAccess CheckDelete(PacmanRepository repository, Actor actor) =>
        CheckOwnership(repository, actor, ActionNames.Delete);

    /// <summary>
    /// The ownership rule every change to an existing repository is held to: the system and the
    /// owner may, anyone else may not. The actor's scope must permit <paramref name="action"/>
    /// first.
    /// </summary>
    private static RepositoryAccess CheckOwnership(PacmanRepository repository, Actor actor, string action)
    {
        if (actor.IsSystem)
        {
            return RepositoryAccess.Allowed;
        }

        if (actor.User is not { } user)
        {
            return RepositoryAccess.Unauthenticated;
        }

        if (!ScopePermits(actor, action))
        {
            return RepositoryAccess.Forbidden;
        }

        if (repository.OwnerId == user.Id)
        {
            return RepositoryAccess.Allowed;
        }

        // A public repository is already known to exist, so admitting that and refusing the change
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
    public RepositoryAccess CheckCreate(Actor actor)
    {
        if (actor.User is null)
        {
            return RepositoryAccess.Unauthenticated;
        }

        return ScopePermits(actor, ActionNames.Create) ? RepositoryAccess.Allowed : RepositoryAccess.Forbidden;
    }

    /// <summary>
    /// Whether the actor's scope permits <paramref name="action"/> on repositories, together with
    /// reading them, which every change needs.
    /// </summary>
    private static bool ScopePermits(Actor actor, string action) =>
        actor.Scope.Permits(EntityNames.Repositories, action)
        && actor.Scope.Permits(EntityNames.Repositories, ActionNames.Read);
}

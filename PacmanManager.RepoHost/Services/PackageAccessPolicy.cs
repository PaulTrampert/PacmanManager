using System.Linq.Expressions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using static PacmanManager.RepoHost.Authentication.ScopeValues;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// The single definition of who may publish, replace or remove packages in a repository.
/// </summary>
/// <remarks>
/// <para>
/// Packages have no visibility of their own: a package is visible exactly when its repository is,
/// so <see cref="VisibleTo"/> composes <see cref="RepositoryAccessPolicy.VisibleTo"/> rather than
/// restating it. What it adds is the scope: an actor that may not read packages sees the packages
/// in the repositories an anonymous caller sees.
/// </para>
/// <para>
/// Every verdict asks the actor's <see cref="Actor.Scope"/> after the no-user rule and before the
/// repository grant, and is <see cref="RepositoryAccess.Forbidden"/> unless the scope permits the
/// change together with <c>packages:read</c> and <c>repositories:read</c>.
/// </para>
/// <para>
/// Like <see cref="RepositoryAccessPolicy"/>, it is intentionally free of any database, HTTP or
/// logging dependency, which makes every rule a plain unit test.
/// </para>
/// <para>
/// <b>The <c>Publish</c> permission is held over a repository, not over a package.</b> Anyone
/// holding it on a repository may publish, replace or remove <i>any</i> package in that
/// repository; a package's publisher is recorded to say who last pushed it and confers no rights
/// of its own. Deciding the question at the repository, where the grant was actually made, stops
/// a routine republish from silently transferring a claim over a package between users. Until a
/// grant table exists, holding <c>Publish</c> on a repository means owning it.
/// </para>
/// </remarks>
/// <param name="repositoryPolicy">The repository visibility that package visibility is built on.</param>
internal sealed class PackageAccessPolicy(RepositoryAccessPolicy repositoryPolicy)
{
    /// <summary>
    /// Builds the predicate describing every repository whose packages <paramref name="actor"/> is
    /// allowed to see.
    /// </summary>
    /// <param name="actor">The actor to build visibility for.</param>
    /// <returns>An expression over the repository, suitable for composition into an EF Core query.</returns>
    /// <remarks>
    /// This is <see cref="RepositoryAccessPolicy.VisibleTo"/> when the actor's scope permits
    /// <c>packages:read</c>, and what it is for <see cref="Actor.Anonymous"/> when it does not.
    /// Reading a package therefore needs <c>repositories:read</c> as well, because
    /// <see cref="RepositoryAccessPolicy.VisibleTo"/> asks for it in turn.
    /// </remarks>
    public Expression<Func<PacmanRepository, bool>> VisibleTo(Actor actor) =>
        repositoryPolicy.VisibleTo(
            actor.Scope.Permits(EntityNames.Packages, ActionNames.Read) ? actor : Actor.Anonymous);

    /// <summary>
    /// Decides whether <paramref name="actor"/> may publish a package into
    /// <paramref name="repository"/>, or replace one already published there.
    /// </summary>
    /// <param name="repository">The repository the package is published into.</param>
    /// <param name="actor">The actor attempting the publish.</param>
    /// <returns>The outcome of the check.</returns>
    /// <remarks>
    /// <para>
    /// There is no package argument because the permission is repository-scoped, which is also why
    /// one method answers for a first publish and a replacement alike.
    /// </para>
    /// <para>
    /// This is deliberately separate from <see cref="RepositoryAccessPolicy.CheckUpdate"/> even
    /// though the two agree today: "may publish packages here" and "may edit the repository
    /// itself" are different questions that a grant table will answer differently.
    /// </para>
    /// <para>
    /// Callers are expected to have already restricted the repository to the actor's visible set,
    /// so a private repository reaching this method belongs to the actor. The
    /// <see cref="RepositoryAccess.NotFound"/> result is a second line of defence for callers that
    /// did not.
    /// </para>
    /// </remarks>
    public RepositoryAccess CheckPublish(PacmanRepository repository, Actor actor) =>
        CheckRepositoryGrant(repository, actor, ActionNames.Create);

    /// <summary>
    /// Decides whether <paramref name="actor"/> may delete a package from
    /// <paramref name="repository"/>.
    /// </summary>
    /// <param name="repository">The repository the package lives in.</param>
    /// <param name="actor">The actor attempting the delete.</param>
    /// <returns>The outcome of the check.</returns>
    /// <remarks>
    /// <para>
    /// There is no package argument because the permission is repository-scoped: whoever published
    /// the package is irrelevant to it.
    /// </para>
    /// <para>
    /// This is a verdict of its own rather than a second use of <see cref="CheckPublish"/>, although
    /// the two agree today: a build pipeline that publishes need not be able to delete.
    /// </para>
    /// <para>
    /// Callers are expected to have already restricted the repository to the actor's visible set,
    /// so a private repository reaching this method belongs to the actor. The
    /// <see cref="RepositoryAccess.NotFound"/> result is a second line of defence for callers that
    /// did not.
    /// </para>
    /// </remarks>
    public RepositoryAccess CheckDelete(PacmanRepository repository, Actor actor) =>
        CheckRepositoryGrant(repository, actor, ActionNames.Delete);

    /// <summary>
    /// The rule every change to a repository's packages is held to until a grant table exists: the
    /// system and the repository's owner may, anyone else may not. The actor's scope must permit
    /// <paramref name="action"/> on packages first, and reading both packages and repositories.
    /// </summary>
    private static RepositoryAccess CheckRepositoryGrant(PacmanRepository repository, Actor actor, string action)
    {
        if (actor.IsSystem)
        {
            return RepositoryAccess.Allowed;
        }

        if (actor.User is not { } user)
        {
            return RepositoryAccess.Unauthenticated;
        }

        if (!actor.Scope.Permits(EntityNames.Packages, action)
            || !actor.Scope.Permits(EntityNames.Packages, ActionNames.Read)
            || !actor.Scope.Permits(EntityNames.Repositories, ActionNames.Read))
        {
            return RepositoryAccess.Forbidden;
        }

        if (repository.OwnerId == user.Id)
        {
            return RepositoryAccess.Allowed;
        }

        // A public repository is already known to exist, so admitting that and refusing the
        // change leaks nothing. A private one must keep pretending it is not there.
        return repository.IsPublic ? RepositoryAccess.Forbidden : RepositoryAccess.NotFound;
    }
}

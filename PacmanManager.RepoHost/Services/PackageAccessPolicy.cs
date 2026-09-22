using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// The single definition of who may publish, replace or remove packages in a repository.
/// </summary>
/// <remarks>
/// <para>
/// Packages have no visibility of their own: a package is visible exactly when its repository is,
/// so reads compose <see cref="RepositoryAccessPolicy.VisibleTo"/> rather than restating it. This
/// type therefore holds only what is genuinely new — the verdicts for publishing and deleting.
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
internal sealed class PackageAccessPolicy
{
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
        CheckRepositoryGrant(repository, actor);

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
        CheckRepositoryGrant(repository, actor);

    /// <summary>
    /// The rule every change to a repository's packages is held to until a grant table exists: the
    /// system and the repository's owner may, anyone else may not.
    /// </summary>
    private static RepositoryAccess CheckRepositoryGrant(PacmanRepository repository, Actor actor)
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

        // A public repository is already known to exist, so admitting that and refusing the
        // change leaks nothing. A private one must keep pretending it is not there.
        return repository.IsPublic ? RepositoryAccess.Forbidden : RepositoryAccess.NotFound;
    }
}

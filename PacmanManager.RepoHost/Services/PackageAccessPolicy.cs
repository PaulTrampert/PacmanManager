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
/// type therefore holds only what is genuinely new — the <c>Publish</c> permission.
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
    /// Decides whether <paramref name="actor"/> may publish, replace or delete a package in
    /// <paramref name="repository"/>.
    /// </summary>
    /// <param name="repository">The repository the package lives in.</param>
    /// <param name="actor">The actor attempting the publish or delete.</param>
    /// <returns>The outcome of the check.</returns>
    /// <remarks>
    /// <para>
    /// There is no package argument because the permission is repository-scoped, which is also why
    /// one method answers for publish, replace and delete alike.
    /// </para>
    /// <para>
    /// This is deliberately separate from <see cref="RepositoryAccessPolicy.CheckWrite"/> even
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
    public RepositoryAccess CheckPublish(PacmanRepository repository, Actor actor)
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
        // publish leaks nothing. A private one must keep pretending it is not there.
        return repository.IsPublic ? RepositoryAccess.Forbidden : RepositoryAccess.NotFound;
    }
}

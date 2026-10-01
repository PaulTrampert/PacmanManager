using PacmanManager.RepoHost.Authentication;
using static PacmanManager.RepoHost.Authentication.ScopeValues;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// The single definition of who may read and change the current user's own record.
/// </summary>
/// <remarks>
/// <para>
/// Every verdict has the same arms, in order: a system actor is allowed, an actor with no user is
/// <see cref="RepositoryAccess.Unauthenticated"/>, and an actor whose <see cref="Actor.Scope"/> does
/// not permit the action on <c>users</c>, together with <c>users:read</c>, is
/// <see cref="RepositoryAccess.Forbidden"/>.
/// </para>
/// <para>
/// There are no ownership rules underneath, because <c>me</c> is the only subject either verdict
/// answers for. For the same reason no verdict answers <see cref="RepositoryAccess.NotFound"/>.
/// </para>
/// <para>
/// Only the current user has a verdict. Reading other users, by listing them or by id, serves
/// anonymous routes, so a credential without <c>users:read</c> reads them exactly as a stranger
/// does, and nothing here is asked.
/// </para>
/// <para>
/// Like <see cref="RepositoryAccessPolicy"/>, it is intentionally free of any database, HTTP or
/// logging dependency, which makes every rule a plain unit test.
/// </para>
/// </remarks>
internal sealed class UserAccessPolicy
{
    /// <summary>
    /// Decides whether <paramref name="actor"/> may read its own user record.
    /// </summary>
    /// <param name="actor">The actor attempting the read.</param>
    /// <returns>The outcome of the check.</returns>
    public RepositoryAccess CheckReadCurrent(Actor actor) => Check(actor, ActionNames.Read);

    /// <summary>
    /// Decides whether <paramref name="actor"/> may change its own user record.
    /// </summary>
    /// <param name="actor">The actor attempting the change.</param>
    /// <returns>The outcome of the check.</returns>
    public RepositoryAccess CheckUpdateCurrent(Actor actor) => Check(actor, ActionNames.Update);

    /// <summary>
    /// The rule every user verdict is held to: the system may, an actor with no user is challenged,
    /// and anyone else may when its scope permits <paramref name="action"/> on users and reading
    /// them.
    /// </summary>
    private static RepositoryAccess Check(Actor actor, string action)
    {
        if (actor.IsSystem)
        {
            return RepositoryAccess.Allowed;
        }

        if (actor.User is null)
        {
            return RepositoryAccess.Unauthenticated;
        }

        return actor.Scope.Permits(EntityNames.Users, action)
               && actor.Scope.Permits(EntityNames.Users, ActionNames.Read)
            ? RepositoryAccess.Allowed
            : RepositoryAccess.Forbidden;
    }
}

using PacmanManager.RepoHost.Authentication;
using static PacmanManager.RepoHost.Authentication.ScopeValues;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// The single definition of who may list, mint and revoke access tokens.
/// </summary>
/// <remarks>
/// <para>
/// Every verdict has the same arms, in order: a system actor is allowed, an actor with no user is
/// <see cref="RepositoryAccess.Unauthenticated"/>, and an actor whose <see cref="Actor.Scope"/> does
/// not permit the action on <c>tokens</c>, together with <c>tokens:read</c>, is
/// <see cref="RepositoryAccess.Forbidden"/>.
/// </para>
/// <para>
/// There are no ownership rules underneath. Every token method's subject is the actor's own tokens,
/// so another user's token is never in the set <see cref="AccessTokenService"/> queries, and is a
/// miss there rather than a verdict here. For the same reason no verdict answers
/// <see cref="RepositoryAccess.NotFound"/>.
/// </para>
/// <para>
/// Tokens have no anonymous view, so unlike repositories and packages a read has a verdict of its
/// own: a credential that may not read tokens is refused rather than shown less.
/// </para>
/// <para>
/// Like <see cref="RepositoryAccessPolicy"/>, it is intentionally free of any database, HTTP or
/// logging dependency, which makes every rule a plain unit test.
/// </para>
/// </remarks>
internal sealed class AccessTokenAccessPolicy
{
    /// <summary>
    /// Decides whether <paramref name="actor"/> may list its own access tokens.
    /// </summary>
    /// <param name="actor">The actor attempting the listing.</param>
    /// <returns>The outcome of the check.</returns>
    public RepositoryAccess CheckRead(Actor actor) => Check(actor, ActionNames.Read);

    /// <summary>
    /// Decides whether <paramref name="actor"/> may mint an access token for itself.
    /// </summary>
    /// <param name="actor">The actor attempting the mint.</param>
    /// <returns>The outcome of the check.</returns>
    public RepositoryAccess CheckCreate(Actor actor) => Check(actor, ActionNames.Create);

    /// <summary>
    /// Decides whether <paramref name="actor"/> may revoke one of its own access tokens.
    /// </summary>
    /// <param name="actor">The actor attempting the revocation.</param>
    /// <returns>The outcome of the check.</returns>
    public RepositoryAccess CheckDelete(Actor actor) => Check(actor, ActionNames.Delete);

    /// <summary>
    /// The rule every token verdict is held to: the system may, an actor with no user is challenged,
    /// and anyone else may when its scope permits <paramref name="action"/> on tokens and reading
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

        return actor.Scope.Permits(EntityNames.Tokens, action)
               && actor.Scope.Permits(EntityNames.Tokens, ActionNames.Read)
            ? RepositoryAccess.Allowed
            : RepositoryAccess.Forbidden;
    }
}

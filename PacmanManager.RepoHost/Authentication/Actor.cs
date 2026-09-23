using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Authentication;

/// <summary>
/// The identity a unit of work is executing as.
/// </summary>
/// <remarks>
/// An actor is deliberately not tied to HTTP. A web request produces an actor from the
/// authenticated principal, while a CLI tool or background job supplies one directly, which
/// lets services enforce the same authorization rules in both settings.
/// </remarks>
public sealed record Actor
{
    private Actor(User? user, bool isSystem, ActorScope scope)
    {
        User = user;
        IsSystem = isSystem;
        Scope = scope;
    }

    /// <summary>
    /// The user this work is being performed on behalf of, or <c>null</c> when there is none.
    /// </summary>
    public User? User { get; }

    /// <summary>
    /// Whether this actor bypasses ownership and visibility restrictions. Only ever true for
    /// trusted, non-interactive hosts such as CLI tools and migrations.
    /// </summary>
    public bool IsSystem { get; }

    /// <summary>
    /// Whether this actor represents any identity at all.
    /// </summary>
    public bool IsAuthenticated => User is not null || IsSystem;

    /// <summary>
    /// What the credential behind this actor may be used for. A ceiling on what <see cref="User"/>
    /// may do, never a grant: the ownership and visibility rules still apply underneath it.
    /// </summary>
    public ActorScope Scope { get; }

    /// <summary>
    /// Whether this actor's scope permits nothing but reading.
    /// </summary>
    public bool IsReadOnly => !Scope.PermitsAnyActionButRead;

    /// <summary>
    /// An unidentified caller. Sees only public data and may not write anything.
    /// </summary>
    /// <remarks>
    /// Its scope is <see cref="ActorScope.Empty"/>, which no verdict consults: with no user, every
    /// verdict answers before it reaches the scope.
    /// </remarks>
    public static readonly Actor Anonymous = new(null, isSystem: false, ActorScope.Empty);

    /// <summary>
    /// A trusted host with no associated user. Sees everything, but cannot create repositories
    /// because there is no user to own them; use <see cref="SystemFor"/> for that.
    /// </summary>
    /// <remarks>
    /// There is no credential behind it, so its scope is <see cref="ActorScope.Unrestricted"/>.
    /// </remarks>
    public static readonly Actor System = new(null, isSystem: true, ActorScope.Unrestricted);

    /// <summary>
    /// An actor representing a specific user, subject to the normal authorization rules.
    /// </summary>
    /// <param name="user">The user to act as.</param>
    /// <param name="scope">
    /// What the credential the user presented may be used for, or <see cref="ActorScope.Unrestricted"/>
    /// when there is no credential behind the actor.
    /// </param>
    public static Actor For(User user, ActorScope scope) => new(user, isSystem: false, scope);

    /// <summary>
    /// A trusted host acting on behalf of a specific user. Bypasses authorization checks, but
    /// still attributes ownership of anything it creates to <paramref name="user"/>.
    /// </summary>
    /// <param name="user">The user to attribute work to.</param>
    /// <remarks>
    /// There is no credential behind it, so its scope is <see cref="ActorScope.Unrestricted"/>.
    /// </remarks>
    public static Actor SystemFor(User user) => new(user, isSystem: true, ActorScope.Unrestricted);
}

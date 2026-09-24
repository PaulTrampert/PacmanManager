using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Supplies a predetermined <see cref="Actor"/>. Intended for CLI tools, background jobs and
/// tests, which know up front who they are acting as.
/// </summary>
/// <param name="actor">The actor to supply.</param>
public class FixedActorAccessor(Actor actor) : IActorAccessor
{
    /// <summary>
    /// An accessor for a trusted host with no associated user.
    /// </summary>
    public static FixedActorAccessor System { get; } = new(Actor.System);

    /// <summary>
    /// An accessor for an unidentified caller.
    /// </summary>
    public static FixedActorAccessor Anonymous { get; } = new(Actor.Anonymous);

    /// <summary>
    /// An accessor for a specific user, held to the normal authorization rules.
    /// </summary>
    /// <param name="user">The user to act as.</param>
    /// <returns>
    /// An accessor whose actor has <see cref="ActorScope.Unrestricted"/> scope: there is no
    /// credential behind it to narrow what <paramref name="user"/> may do.
    /// </returns>
    public static FixedActorAccessor For(User user) => new(Actor.For(user, ActorScope.Unrestricted));

    /// <inheritdoc />
    public ValueTask<Actor> GetActorAsync(CancellationToken ct = default) => ValueTask.FromResult(actor);
}

using PacmanManager.RepoHost.Authentication;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Supplies the <see cref="Actor"/> that the current scope is executing as.
/// </summary>
/// <remarks>
/// No implementation is registered by default, so a host that fails to choose one fails at
/// dependency resolution rather than silently running as an unidentified caller.
/// </remarks>
public interface IActorAccessor
{
    /// <summary>
    /// Gets the actor for the current scope.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The current actor. Never null; use <see cref="Actor.Anonymous"/> for no identity.</returns>
    ValueTask<Actor> GetActorAsync(CancellationToken ct = default);
}

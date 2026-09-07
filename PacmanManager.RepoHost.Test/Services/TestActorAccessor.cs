using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// An <see cref="IActorAccessor"/> whose actor a test can change between arrange and act.
/// </summary>
/// <remarks>
/// <see cref="FixedActorAccessor"/> takes its actor at construction, which would mean rebuilding
/// the service under test every time a fixture wants to ask "and what does somebody else see?".
/// </remarks>
internal sealed class TestActorAccessor : IActorAccessor
{
    /// <summary>
    /// The actor to supply. Defaults to an unidentified caller.
    /// </summary>
    public Actor Actor { get; set; } = Actor.Anonymous;

    /// <inheritdoc />
    public ValueTask<Actor> GetActorAsync(CancellationToken ct = default) => ValueTask.FromResult(Actor);
}

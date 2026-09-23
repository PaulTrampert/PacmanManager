namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Thrown when the current actor's credential does not carry the scope an operation needs, on a
/// route with no anonymous view to fall back to.
/// </summary>
/// <remarks>
/// Repositories and packages never raise this: a credential that may not read them reads them as a
/// stranger would, and a change they refuse is reported through their own exceptions. It is for
/// entities such as the current user and their tokens, which have nothing to show a stranger.
/// </remarks>
/// <param name="entity">The entity the operation was refused on, as named in the <c>scope</c> grammar.</param>
/// <param name="action">The action refused, as named in the <c>scope</c> grammar.</param>
public class InsufficientScopeException(string entity, string action)
    : Exception($"The credential's scope does not permit '{action}' on '{entity}'.")
{
    /// <summary>
    /// The entity the operation was refused on, such as <c>tokens</c>.
    /// </summary>
    public string Entity { get; } = entity;

    /// <summary>
    /// The action refused, such as <c>create</c>.
    /// </summary>
    public string Action { get; } = action;
}

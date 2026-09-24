namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Thrown when the current actor's credential does not carry the scope an operation needs, where
/// there is no existing entity to report the refusal against.
/// </summary>
/// <remarks>
/// A read of a repository or package never raises this: a credential that may not read them reads
/// them as a stranger would. A refused change to an existing repository or package is reported
/// through <see cref="RepositoryForbiddenException"/> or <see cref="PackageForbiddenException"/>,
/// which name it. Creating a repository has no repository to name, so a scope refusal of it raises
/// this, as do the current user and their tokens, which have nothing to show a stranger.
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

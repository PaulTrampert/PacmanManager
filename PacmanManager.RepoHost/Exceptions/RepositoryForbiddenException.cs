namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Thrown when the current actor may see a repository but is not permitted to modify it.
/// </summary>
/// <remarks>
/// This is only ever raised for repositories whose existence the actor is already entitled to
/// know about. Where revealing existence would itself leak information, the operation reports the
/// repository as missing instead.
/// </remarks>
/// <param name="repositoryId">The repository the actor was denied access to.</param>
public class RepositoryForbiddenException(Guid repositoryId)
    : Exception($"Repository '{repositoryId}' is not owned by the current user.")
{
    /// <summary>
    /// The repository the actor was denied access to.
    /// </summary>
    public Guid RepositoryId { get; } = repositoryId;
}

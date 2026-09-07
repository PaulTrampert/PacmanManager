namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Thrown when the current actor may see a repository but is not permitted to publish, replace or
/// remove packages in it.
/// </summary>
/// <remarks>
/// <para>
/// This is only ever raised for repositories whose existence the actor is already entitled to know
/// about. Where revealing existence would itself leak information, the operation reports the
/// package as missing instead.
/// </para>
/// <para>
/// The exception names the repository rather than the package because the permission that was
/// missing is held over the repository.
/// </para>
/// </remarks>
/// <param name="repositoryId">The repository the actor was denied publish access to.</param>
public class PackageForbiddenException(Guid repositoryId)
    : Exception($"The current user may not publish packages to repository '{repositoryId}'.")
{
    /// <summary>
    /// The repository the actor was denied publish access to.
    /// </summary>
    public Guid RepositoryId { get; } = repositoryId;
}

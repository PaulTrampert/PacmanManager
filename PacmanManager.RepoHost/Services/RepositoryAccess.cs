namespace PacmanManager.RepoHost.Services;

/// <summary>
/// The outcome of an authorization check against a repository.
/// </summary>
internal enum RepositoryAccess
{
    /// <summary>The actor may perform the operation.</summary>
    Allowed,

    /// <summary>
    /// The repository exists but the actor may not know that, so the operation must behave
    /// exactly as though the repository were absent.
    /// </summary>
    NotFound,

    /// <summary>
    /// The actor may see the repository but not modify it.
    /// </summary>
    Forbidden,

    /// <summary>
    /// The operation requires an identity and the actor has none.
    /// </summary>
    Unauthenticated,
}

namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Thrown when the current actor is identified but may not create a repository, because its
/// credential's scope does not permit it.
/// </summary>
/// <remarks>
/// Distinct from <see cref="NoCurrentUserException"/>, which means there is nobody to own the new
/// repository: re-authenticating with the same credential would not help here.
/// </remarks>
public class RepositoryCreationForbiddenException()
    : Exception("The current credential may not create repositories.");

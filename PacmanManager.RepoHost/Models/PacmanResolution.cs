namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The outcome of resolving a <c>/pacman/{repoName}/{repoArch}/{fileName}</c> path: the file, or
/// which of the two ways there was none.
/// </summary>
/// <remarks>
/// <para>
/// The route answers the two misses differently. A repository the caller cannot see is a
/// <c>401</c> challenge for an anonymous caller, because pacman sends the credentials in its
/// <c>Server</c> URL only once challenged, and a <c>404</c> for anyone else. A missing file in a
/// repository the caller <i>can</i> see is always a <c>404</c>, since challenging there would only
/// make pacman retry a request that cannot succeed.
/// </para>
/// <para>
/// A repository that does not exist and one the caller may not see are deliberately the same
/// outcome, so nothing downstream can tell them apart.
/// </para>
/// </remarks>
public sealed record PacmanResolution
{
    private PacmanResolution(bool repositoryFound, RepositoryFile? file)
    {
        RepositoryFound = repositoryFound;
        File = file;
    }

    /// <summary>
    /// Whether the repository was found among those the current actor may see. False both when it
    /// does not exist and when it is private to somebody else.
    /// </summary>
    public bool RepositoryFound { get; }

    /// <summary>
    /// The resolved file, or null when there is none.
    /// </summary>
    public RepositoryFile? File { get; }

    /// <summary>
    /// No repository of that name that the current actor may see.
    /// </summary>
    public static PacmanResolution RepositoryNotFound { get; } = new(repositoryFound: false, file: null);

    /// <summary>
    /// The repository is visible, but it does not support the architecture, the name is not one it
    /// serves, or the file is not on disk.
    /// </summary>
    public static PacmanResolution FileNotFound { get; } = new(repositoryFound: true, file: null);

    /// <summary>
    /// A file in a visible repository, or <see cref="FileNotFound"/> when <paramref name="file"/> is
    /// null.
    /// </summary>
    /// <param name="file">The resolved file, if any.</param>
    /// <returns>The resolution.</returns>
    public static PacmanResolution Of(RepositoryFile? file) =>
        file is null ? FileNotFound : new PacmanResolution(repositoryFound: true, file);
}

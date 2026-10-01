namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The outcome of resolving a <c>/pacman/{repoName}/{repoArch}/{fileName}</c> path: the file, a
/// redirect or <c>410</c> for a renamed repository's old name, or which of the two ways there was none.
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
/// <para>
/// A repository's old name, after a rename, is a redirect to the same file under its current name
/// while the redirect window lasts and <see cref="Gone"/> afterwards. Both are only ever produced for
/// a repository the caller may see.
/// </para>
/// </remarks>
public sealed record PacmanResolution
{
    private PacmanResolution(
        bool repositoryFound,
        RepositoryFile? file,
        PacmanRedirect? redirect = null,
        bool isGone = false)
    {
        RepositoryFound = repositoryFound;
        File = file;
        Redirect = redirect;
        IsGone = isGone;
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
    /// Where the same file is now, when the path named a repository by a name it was renamed away
    /// from and that name still redirects.
    /// </summary>
    public PacmanRedirect? Redirect { get; }

    /// <summary>
    /// Whether the path named a repository by a name it was renamed away from that no longer
    /// redirects.
    /// </summary>
    public bool IsGone { get; }

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

    /// <summary>
    /// A redirect to the same file under the repository's current name.
    /// </summary>
    /// <param name="redirect">The repository name and file name to redirect to.</param>
    /// <returns>The resolution.</returns>
    public static PacmanResolution RedirectTo(PacmanRedirect redirect) =>
        new(repositoryFound: true, file: null, redirect: redirect);

    /// <summary>
    /// A retired name whose redirect has lapsed.
    /// </summary>
    public static PacmanResolution Gone { get; } = new(repositoryFound: true, file: null, isGone: true);
}

/// <summary>
/// Where a request for a renamed repository's file is sent: the same architecture, under the
/// repository's current name.
/// </summary>
/// <param name="RepoName">The repository's current name.</param>
/// <param name="FileName">
/// The requested file under the current name: a database requested by the old name is named for the
/// new one, and anything else keeps the name it was requested by.
/// </param>
public sealed record PacmanRedirect(string RepoName, string FileName);

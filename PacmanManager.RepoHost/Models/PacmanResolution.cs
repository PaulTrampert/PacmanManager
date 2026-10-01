namespace PacmanManager.RepoHost.Models;

/// <summary>
/// What a path under the <c>/pacman</c> route root resolved to, when it resolved to anything.
/// </summary>
/// <remarks>
/// Exactly one of the three outcomes holds: a file to serve, a redirect to the same file under a
/// renamed repository's current name, or a name that was renamed away from and no longer redirects.
/// Build one with <see cref="Serve"/>, <see cref="RedirectTo"/> or <see cref="Gone"/>.
/// </remarks>
public sealed record PacmanResolution
{
    private PacmanResolution()
    {
    }

    /// <summary>
    /// The file to serve, when the path named a file of a live repository.
    /// </summary>
    public RepositoryFile? File { get; private init; }

    /// <summary>
    /// Where the same file is now, when the path named a repository by a name it was renamed away
    /// from and that name still redirects.
    /// </summary>
    public PacmanRedirect? Redirect { get; private init; }

    /// <summary>
    /// Whether the path named a repository by a name it was renamed away from that no longer
    /// redirects.
    /// </summary>
    public bool IsGone { get; private init; }

    /// <summary>
    /// A file to serve.
    /// </summary>
    /// <param name="file">The open file and its modification time.</param>
    public static PacmanResolution Serve(RepositoryFile file) => new() { File = file };

    /// <summary>
    /// A redirect to the same file under the repository's current name.
    /// </summary>
    /// <param name="redirect">The repository name and file name to redirect to.</param>
    public static PacmanResolution RedirectTo(PacmanRedirect redirect) => new() { Redirect = redirect };

    /// <summary>
    /// A retired name whose redirect has lapsed.
    /// </summary>
    public static PacmanResolution Gone { get; } = new() { IsGone = true };
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

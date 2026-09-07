namespace PacmanManager.RepoHost.CliTools;

/// <summary>
/// The location of a repository's pacman database file. The pacman database tools (<see cref="RepoAdd"/> and
/// <see cref="RepoRemove"/>) each hold one of these, so the <c>.db.tar.gz</c> file name and the directory the tools
/// run in are defined once and cannot drift apart between them.
/// </summary>
/// <param name="name">Name of the repository database, without extension. This is the repository id.</param>
/// <param name="repoHome">The libalpm database home directory. Its <c>sync</c> subdirectory holds the database.</param>
public class RepositoryDatabase(string name, string repoHome)
{
    /// <summary>
    /// Extension shared by every repository database file.
    /// </summary>
    public const string FileExtension = ".db.tar.gz";

    /// <summary>
    /// File name of the repository database, relative to <see cref="SyncDirectory"/>.
    /// </summary>
    public string FileName => $"{name}{FileExtension}";

    /// <summary>
    /// The <c>sync</c> directory of the libalpm database home, which is where registered sync databases live and
    /// where the database tools are run.
    /// </summary>
    public string SyncDirectory => $"{repoHome}/sync";
}

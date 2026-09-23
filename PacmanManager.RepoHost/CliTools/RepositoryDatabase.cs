namespace PacmanManager.RepoHost.CliTools;

/// <summary>
/// The location of one architecture's pacman database for a repository. The pacman database tools
/// (<see cref="RepoAdd"/> and <see cref="RepoRemove"/>) each hold one of these, so the <c>.db.tar.gz</c> file name and
/// the directory the tools run in are defined once and cannot drift apart between them.
/// </summary>
/// <remarks>
/// A repository supports a set of architectures and <c>repo-add</c> writes one database per architecture, so the
/// architecture is part of the location: each has its own directory, and the file inside it keeps the repository's id
/// as its name.
/// </remarks>
/// <param name="name">Name of the repository database, without extension. This is the repository id.</param>
/// <param name="repoHome">The libalpm database home directory. Its <c>sync</c> subdirectory holds the databases.</param>
/// <param name="architecture">The architecture this database lists packages for. Never <c>any</c>.</param>
public class RepositoryDatabase(string name, string repoHome, string architecture)
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
    /// The architecture's directory under the <c>sync</c> directory of the libalpm database home, which is where the
    /// database lives and where the database tools are run.
    /// </summary>
    public string SyncDirectory => $"{repoHome}/sync/{architecture}";

    /// <summary>
    /// Absolute path of the repository database file.
    /// </summary>
    public string FilePath => $"{SyncDirectory}/{FileName}";
}

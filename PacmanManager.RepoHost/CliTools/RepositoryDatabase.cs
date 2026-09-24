namespace PacmanManager.RepoHost.CliTools;

/// <summary>
/// One architecture's pacman database for a repository, and every file <c>repo-add</c> writes for it. The pacman
/// database tools (<see cref="RepoAdd"/> and <see cref="RepoRemove"/>) each hold one of these, so the file names and
/// the directory the tools run in are defined once and cannot drift apart between them.
/// </summary>
/// <remarks>
/// <para>
/// Everything a repository owns lives under its own directory, <c>{DATA_DIR}/repositories/{id}</c>, and each
/// architecture's database lives in <c>db/{architecture}</c> beneath it. A single <c>repo-add</c> run there produces
/// the sync database <c>{id}.db.tar.gz</c>, the files database <c>{id}.files.tar.gz</c>, the symlinks <c>{id}.db</c>
/// and <c>{id}.files</c> pointing at them, and a <c>.old</c> backup of each database — the <see cref="FileNames"/>.
/// </para>
/// <para>
/// The files keep the repository's id as their name, not the repository's name, so that renaming a repository never
/// has to rename its databases.
/// </para>
/// </remarks>
/// <param name="name">Name of the repository database, without extension. This is the repository id.</param>
/// <param name="repositoryDirectory">
/// The repository's own directory, as <see cref="Infrastructure.IPackagePathResolver.GetRepositoryDirectory"/>
/// reports it. The database lives in its <c>db/{architecture}</c> subdirectory.
/// </param>
/// <param name="architecture">The architecture this database lists packages for. Never <c>any</c>.</param>
public class RepositoryDatabase(string name, string repositoryDirectory, string architecture)
{
    /// <summary>
    /// Extension of the sync database, the file <c>pacman -Sy</c> reads.
    /// </summary>
    public const string FileExtension = ".db.tar.gz";

    /// <summary>
    /// Extension of the files database, the file <c>pacman -Fy</c> reads.
    /// </summary>
    public const string FilesFileExtension = ".files.tar.gz";

    /// <summary>
    /// Suffix <c>repo-add</c> gives the backup it keeps of each database when it rewrites it.
    /// </summary>
    public const string BackupSuffix = ".old";

    /// <summary>
    /// The architecture's directory under the repository's <c>db</c> directory, which is where every file of this
    /// database lives and where the database tools are run.
    /// </summary>
    public string DatabaseDirectory => $"{repositoryDirectory}/db/{architecture}";

    /// <summary>
    /// File name of the sync database, relative to <see cref="DatabaseDirectory"/>.
    /// </summary>
    public string FileName => $"{name}{FileExtension}";

    /// <summary>
    /// Absolute path of the sync database.
    /// </summary>
    public string FilePath => $"{DatabaseDirectory}/{FileName}";

    /// <summary>
    /// File name of the files database, relative to <see cref="DatabaseDirectory"/>.
    /// </summary>
    public string FilesFileName => $"{name}{FilesFileExtension}";

    /// <summary>
    /// Absolute path of the files database.
    /// </summary>
    public string FilesFilePath => $"{DatabaseDirectory}/{FilesFileName}";

    /// <summary>
    /// Every file <c>repo-add</c> writes for this database, relative to <see cref="DatabaseDirectory"/>: the sync and
    /// files databases, the symlinks to each, and the backup of each.
    /// </summary>
    public IEnumerable<string> FileNames =>
    [
        FileName,
        FilesFileName,
        $"{name}.db",
        $"{name}.files",
        $"{FileName}{BackupSuffix}",
        $"{FilesFileName}{BackupSuffix}"
    ];

    /// <summary>
    /// Absolute paths of <see cref="FileNames"/>.
    /// </summary>
    public IEnumerable<string> FilePaths => FileNames.Select(f => $"{DatabaseDirectory}/{f}");
}

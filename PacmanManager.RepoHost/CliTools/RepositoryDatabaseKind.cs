namespace PacmanManager.RepoHost.CliTools;

/// <summary>
/// The two databases <c>repo-add</c> writes for each of a repository's architectures.
/// </summary>
public enum RepositoryDatabaseKind
{
    /// <summary>
    /// The sync database, <see cref="RepositoryDatabase.FileName"/>, which <c>pacman -Sy</c> reads.
    /// </summary>
    Sync,

    /// <summary>
    /// The files database, <see cref="RepositoryDatabase.FilesFileName"/>, which <c>pacman -Fy</c> reads.
    /// </summary>
    Files
}

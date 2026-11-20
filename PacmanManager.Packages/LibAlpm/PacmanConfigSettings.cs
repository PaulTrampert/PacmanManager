namespace PacmanManager.Packages.LibAlpm;

/// <summary>
/// Where under <c>DATA_DIR</c> libalpm and the package store keep their files.
/// </summary>
public class PacmanConfigSettings
{
    /// <summary>
    /// The data directory everything else is under. Defaults to <see cref="EnvironmentVariables.DataDir"/>.
    /// </summary>
    public string DataDir { get; set; } = EnvironmentVariables.DataDir;
    
    /// <summary>
    /// libalpm's database path, the <c>DBPath</c> of the generated configuration.
    /// </summary>
    public string DbPath => Path.Combine(DataDir, "libalpm");

    /// <summary>
    /// libalpm's package cache, the <c>CacheDir</c> of the generated configuration.
    /// </summary>
    public string CacheDir => Path.Combine(DataDir, "libalpm-cache");

    /// <summary>
    /// libalpm's log file, the <c>LogFile</c> of the generated configuration.
    /// </summary>
    public string LogFile => Path.Combine(DataDir, "libalpm.log");

    /// <summary>
    /// The root of the per repository tree, which holds each repository's package files and, under
    /// each one's <c>db</c> directory, the databases <c>repo-add</c> writes for it.
    /// </summary>
    public string RepositoriesDir => Path.Combine(DataDir, "repositories");

    /// <summary>
    /// Where an upload is streamed to while it is being hashed and inspected, before it is moved
    /// into the repository it belongs to.
    /// </summary>
    /// <remarks>
    /// It lives under <see cref="DataDir"/> rather than in the system temporary directory so that
    /// the move into <see cref="RepositoriesDir"/> stays on one filesystem, which keeps it atomic
    /// and free of a second copy of a file that may run to hundreds of megabytes.
    /// </remarks>
    public string TmpDir => Path.Combine(DataDir, "tmp");
}

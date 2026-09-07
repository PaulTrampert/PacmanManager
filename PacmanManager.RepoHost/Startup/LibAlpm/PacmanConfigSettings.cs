namespace PacmanManager.RepoHost.Startup.LibAlpm;

public class PacmanConfigSettings
{
    public string DataDir { get; set; } = EnvironmentVariables.DataDir;
    
    public string DbPath => Path.Combine(DataDir, "libalpm");
    public string CacheDir => Path.Combine(DataDir, "libalpm-cache");
    public string LogFile => Path.Combine(DataDir, "libalpm.log");
    /// <summary>
    /// The root of the per repository tree, which holds each repository's package files (and, via
    /// <see cref="Include"/>, any per repository pacman configuration).
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

    public string Include => Path.Combine(RepositoriesDir, "*", "*.conf");
}

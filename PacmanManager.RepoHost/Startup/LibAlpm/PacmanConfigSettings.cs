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

    public string Include => Path.Combine(RepositoriesDir, "*", "*.conf");
}

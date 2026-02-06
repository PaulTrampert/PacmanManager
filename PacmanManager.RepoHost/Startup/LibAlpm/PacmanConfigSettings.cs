namespace PacmanManager.RepoHost.Startup.LibAlpm;

public class PacmanConfigSettings
{
    public string DataDir { get; set; } = EnvironmentVariables.DataDir;
    
    public string DbPath => Path.Combine(DataDir, "libalpm");
    public string CacheDir => Path.Combine(DataDir, "libalpm-cache");
    public string LogFile => Path.Combine(DataDir, "libalpm.log");
    public string Include => Path.Combine(DataDir, "repositories", "*", "*.conf");
}

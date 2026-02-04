namespace PacmanManager.RepoHost;

/// <summary>
/// Provides access to environment variables used by the application.
/// </summary>
public static class EnvironmentVariables
{
    /// <summary>
    /// Gets the DATA_DIR environment variable value, or "/data" if not set.
    /// </summary>
    public static string DataDir => Environment.GetEnvironmentVariable("DATA_DIR") ?? "/data";
}

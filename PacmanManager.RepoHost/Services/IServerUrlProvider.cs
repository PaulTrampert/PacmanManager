namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Supplies the <c>Server</c> URL a <c>pacman.conf</c> section names to reach any hosted repository.
/// </summary>
public interface IServerUrlProvider
{
    /// <summary>
    /// Builds the server URL, ending in a literal <c>pacman/$repo/$arch</c>, for the current unit of
    /// work.
    /// </summary>
    /// <returns>The absolute server URL, such as <c>https://packages.example.com/pacman/$repo/$arch</c>.</returns>
    string GetServerUrl();
}

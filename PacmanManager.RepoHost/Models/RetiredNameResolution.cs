namespace PacmanManager.RepoHost.Models;

/// <summary>
/// What a request for a name a repository was renamed away from is answered with.
/// </summary>
/// <param name="Repository">
/// The repository that gave the name up, as it is now: under its current name, which is where a
/// redirect points. A repository renamed twice is found under its latest name from either old one.
/// </param>
/// <param name="IsRedirecting">
/// Whether the name still redirects to <paramref name="Repository"/>. Once the redirect window has
/// passed it does not, and the name answers <c>410 Gone</c> instead.
/// </param>
public record RetiredNameResolution(Repository Repository, bool IsRedirecting);

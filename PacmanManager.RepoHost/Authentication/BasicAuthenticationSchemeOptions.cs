using Microsoft.AspNetCore.Authentication;

namespace PacmanManager.RepoHost.Authentication;

/// <summary>
/// Options for the <see cref="AuthnConstants.BasicScheme"/> scheme.
/// </summary>
public class BasicAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The realm named in the <c>WWW-Authenticate: Basic realm="…"</c> challenge.
    /// </summary>
    public string Realm { get; set; } = "pacman";
}

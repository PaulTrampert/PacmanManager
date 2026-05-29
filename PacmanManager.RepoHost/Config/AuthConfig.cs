namespace PacmanManager.RepoHost.Config;

/// <summary>
/// Configurable authentication properties.
/// </summary>
public class AuthConfig
{
    /// <summary>
    /// Config section to find this object in.
    /// </summary>
    public const string Section = "Auth";

    /// <summary>
    /// Name of the authority to authenticate against. Should be the url of an OAuth provider.
    /// </summary>
    public required string Authority { get; init; }
    
    /// <summary>
    /// The audience of the auth token.
    /// </summary>
    public required string Audience { get; init; }
}
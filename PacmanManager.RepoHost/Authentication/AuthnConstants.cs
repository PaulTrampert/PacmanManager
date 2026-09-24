namespace PacmanManager.RepoHost.Authentication;

public static class AuthnConstants
{
    public const string AppUserIdClaimType = "app_user_id";
    public const string SubClaimType = "sub";
    public const string AuthorityClaimType = "iss";
    public const string EmailClaimType = "email";
    public const string DisplayNameClaimType = "preferred_username";

    /// <summary>
    /// The OAuth 2.0 claim saying what a credential may be used for. <c>Program.cs</c> clears the
    /// inbound claim type map, so it reaches the principal under this name rather than remapped.
    /// </summary>
    public const string ScopeClaimType = "scope";

    /// <summary>
    /// The default authentication scheme: a policy scheme that forwards each request to
    /// <see cref="BasicScheme"/> or to the <c>Bearer</c> scheme by the prefix of its
    /// <c>Authorization</c> header. See <see cref="PacmanAuthenticationServiceCollectionExtensions.SelectScheme"/>.
    /// </summary>
    public const string SelectorScheme = "AuthorizationHeaderSelector";

    /// <summary>
    /// The HTTP Basic scheme, which authenticates a <c>PacmanAccessToken</c> presented as a Basic
    /// credential. Handled by <see cref="BasicAuthenticationHandler"/>.
    /// </summary>
    public const string BasicScheme = "Basic";
}

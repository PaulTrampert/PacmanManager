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
}
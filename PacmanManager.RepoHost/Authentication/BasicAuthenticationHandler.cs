using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Authentication;

/// <summary>
/// Authenticates an <c>Authorization: Basic base64(username:password)</c> header carrying a
/// <c>PacmanAccessToken</c>: its identifier in the username and its secret in the password.
/// </summary>
/// <remarks>
/// <para>
/// Verification is <see cref="IUserService.GetUserByAccessTokenAsync"/>'s; this handler only decodes
/// the header and turns the user it returns into a principal. The principal carries
/// <see cref="AuthnConstants.AppUserIdClaimType"/>, so the rest of the pipeline needs no change for
/// it, and exactly one scope value, <c>pacman-manager:*:read</c>, which is the whole of what makes a
/// token unable to write. Nothing on this path talks to the identity provider.
/// </para>
/// <para>
/// Every failure is the same failure, whatever went wrong: the reason is logged, never returned. The
/// client address is attached to every log line written during verification, the service's
/// included, through a logging scope.
/// </para>
/// </remarks>
public class BasicAuthenticationHandler(
    IOptionsMonitor<BasicAuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IUserService userService)
    : AuthenticationHandler<BasicAuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>
    /// The prefix of an <c>Authorization</c> header carrying a Basic credential, trailing space
    /// included. Compared case-insensitively, as HTTP authentication schemes are.
    /// </summary>
    public const string HeaderPrefix = "Basic ";

    /// <summary>
    /// The name of the logging scope property carrying the client address.
    /// </summary>
    public const string ClientAddressScopeKey = "ClientAddress";

    /// <summary>
    /// The failure message of every failed authentication, whatever the reason.
    /// </summary>
    private const string FailureMessage = "Invalid credentials.";

    /// <summary>
    /// Whether <paramref name="request"/> carries a Basic credential, valid or not.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>True when its <c>Authorization</c> header starts with <see cref="HeaderPrefix"/>.</returns>
    public static bool HasBasicCredential(HttpRequest request) =>
        request.Headers.Authorization.ToString().StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!HasBasicCredential(Request))
        {
            return AuthenticateResult.NoResult();
        }

        using var scope = Logger.BeginScope(new Dictionary<string, object?>
        {
            [ClientAddressScopeKey] = Context.Connection.RemoteIpAddress?.ToString(),
        });

        if (!TryDecode(Request.Headers.Authorization.ToString()[HeaderPrefix.Length..], out var username,
                out var password))
        {
            // The content is never logged: it holds the secret, or part of it.
            Logger.LogWarning("Basic credential failed verification: {Reason}", "Undecodable");
            return AuthenticateResult.Fail(FailureMessage);
        }

        var user = await userService.GetUserByAccessTokenAsync(username, password, Context.RequestAborted);
        if (user is null)
        {
            Logger.LogWarning("Basic credential failed verification: {Reason}", "Invalid Token");
            return AuthenticateResult.Fail(FailureMessage);
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(AuthnConstants.AppUserIdClaimType, user.Id.ToString()),
            new Claim(AuthnConstants.ScopeClaimType,
                ScopeValues.For(ScopeValues.Wildcard, ScopeValues.ActionNames.Read)),
        ], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    /// <summary>
    /// Answers with <c>401</c> and <c>WWW-Authenticate: Basic realm="…"</c>, and nothing that says
    /// why.
    /// </summary>
    /// <param name="properties">Unused.</param>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = $"Basic realm=\"{Options.Realm}\"";
        return Task.CompletedTask;
    }

    /// <summary>
    /// Decodes the parameter of a Basic credential into its two fields, per RFC 7617: Base64 of
    /// UTF-8 <c>username:password</c>, split at the first colon.
    /// </summary>
    private static bool TryDecode(string parameter, out string username, out string password)
    {
        username = password = string.Empty;
        var encoded = parameter.Trim();
        var buffer = new byte[encoded.Length];
        if (!Convert.TryFromBase64String(encoded, buffer, out var written))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(buffer, 0, written);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        var colon = decoded.IndexOf(':');
        if (colon < 0)
        {
            return false;
        }

        username = decoded[..colon];
        password = decoded[(colon + 1)..];
        return true;
    }
}

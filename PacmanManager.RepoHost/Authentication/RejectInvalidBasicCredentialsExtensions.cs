using Microsoft.AspNetCore.Authentication;

namespace PacmanManager.RepoHost.Authentication;

/// <summary>
/// Makes a Basic credential that was offered and not accepted a <c>401</c> on every route.
/// </summary>
public static class RejectInvalidBasicCredentialsExtensions
{
    /// <summary>
    /// Adds middleware answering <c>401</c> with <c>WWW-Authenticate: Basic realm="…"</c> to any
    /// request that carries a Basic credential and was not authenticated by it. Only the
    /// <i>absence</i> of a credential falls through to anonymous.
    /// </summary>
    /// <remarks>
    /// Must be added immediately after <c>UseAuthentication()</c> and before <c>UseAuthorization()</c>.
    /// ASP.NET Core discards a failed authentication on an <c>[AllowAnonymous]</c> route and carries on
    /// as anonymous, so without this a mistyped token would be answered as a stranger, with a
    /// <c>404</c> for the user's own private repository. It asks only whether a Basic header was
    /// offered and refused, so it has no opinion about which routes take the scheme.
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder, for chaining.</returns>
    public static IApplicationBuilder UseRejectInvalidBasicCredentials(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.User.Identity?.IsAuthenticated != true
                && BasicAuthenticationHandler.HasBasicCredential(context.Request))
            {
                await context.ChallengeAsync(AuthnConstants.BasicScheme);
                return;
            }

            await next(context);
        });
}

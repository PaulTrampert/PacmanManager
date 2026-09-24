using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace PacmanManager.RepoHost.Authentication;

/// <summary>
/// Registers the application's authentication schemes.
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AuthnConstants.SelectorScheme"/> as the default scheme, forwarding by
    /// <see cref="SelectScheme"/> to the <c>Bearer</c> scheme or to <see cref="AuthnConstants.BasicScheme"/>.
    /// </summary>
    /// <remarks>
    /// The authentication middleware runs only the default scheme, and an <c>[AllowAnonymous]</c>
    /// route suppresses any scheme an <c>[Authorize]</c> attribute names, so a policy scheme choosing
    /// on the header is the one way to get the Basic handler run on every route. No route names a
    /// scheme.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The authentication builder, for chaining.</returns>
    public static AuthenticationBuilder AddPacmanAuthentication(this IServiceCollection services) =>
        services.AddAuthentication(AuthnConstants.SelectorScheme)
            .AddPolicyScheme(AuthnConstants.SelectorScheme, AuthnConstants.SelectorScheme,
                options => options.ForwardDefaultSelector = SelectScheme)
            .AddJwtBearer()
            .AddScheme<BasicAuthenticationSchemeOptions, BasicAuthenticationHandler>(
                AuthnConstants.BasicScheme, _ => { });

    /// <summary>
    /// Chooses the scheme a request is authenticated, challenged and forbidden by.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>
    /// <see cref="AuthnConstants.BasicScheme"/> when the <c>Authorization</c> header starts with
    /// <c>Basic </c>; otherwise, for <c>Bearer</c>, an unrecognised scheme or no header at all, the
    /// <c>Bearer</c> scheme, exactly as before Basic existed.
    /// </returns>
    public static string SelectScheme(HttpContext context) =>
        BasicAuthenticationHandler.HasBasicCredential(context.Request)
            ? AuthnConstants.BasicScheme
            : JwtBearerDefaults.AuthenticationScheme;
}

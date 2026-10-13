using Microsoft.Extensions.Options;
using PacmanManager.RepoHost.Config;
using PacmanManager.RepoHost.Controllers;

namespace PacmanManager.RepoHost.Services;

/// <summary>
/// Builds the server URL from the configured <see cref="PublicUrlConfig.PublicUrl"/>, or, when none
/// is configured, from the current request's root.
/// </summary>
/// <remarks>
/// The request root is the request's <c>Scheme</c>, <c>Host</c> and <c>PathBase</c> as the pipeline
/// sees them after <c>UseForwardedHeaders</c>, so a trusted proxy's <c>X-Forwarded-Proto</c> sets the
/// scheme. A configured <c>PublicUrl</c> always wins. The value is built on every call and never
/// stored, so it follows the setting rather than the address a repository was created under.
/// </remarks>
public class ServerUrlProvider(IOptions<PublicUrlConfig> config, IHttpContextAccessor httpContextAccessor)
    : IServerUrlProvider
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// No <c>PublicUrl</c> is configured and there is no current request to take a root from.
    /// </exception>
    public string GetServerUrl() => $"{NormalizeRoot(GetRoot())}{PacmanController.ServerPath}";

    private string GetRoot()
    {
        var publicUrl = config.Value;
        if (publicUrl.IsConfigured)
            return publicUrl.PublicUrl!;

        var request = httpContextAccessor.HttpContext?.Request
                      ?? throw new InvalidOperationException(
                          $"{nameof(PublicUrlConfig.PublicUrl)} is not configured and there is no current request to build a server URL from.");

        return $"{request.Scheme}://{request.Host.ToUriComponent()}{request.PathBase.ToUriComponent()}";
    }

    /// <summary>
    /// Makes <paramref name="root"/> end in exactly one <c>/</c>, so <c>pacman/</c> can be appended to
    /// a root written either way.
    /// </summary>
    internal static string NormalizeRoot(string root) => root.TrimEnd('/') + "/";
}

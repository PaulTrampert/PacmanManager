using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace PacmanManager.RepoHost.Config;

/// <summary>
/// Builds <see cref="ForwardedHeadersOptions"/> from <see cref="ForwardedHeadersConfig"/>: the
/// <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> headers are believed from loopback and from
/// each configured trusted source.
/// </summary>
/// <remarks>
/// The configured sources are added to ASP.NET Core's defaults, which trust loopback only. The lists
/// are never cleared: an empty list there would trust every caller, letting any client forge the
/// address that is logged.
/// </remarks>
public class ConfigureForwardedHeadersOptions(IOptions<ForwardedHeadersConfig> config)
    : IConfigureOptions<ForwardedHeadersOptions>
{
    /// <inheritdoc />
    public void Configure(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        foreach (var entry in config.Value.TrustedSources)
        {
            // Validation has already rejected anything unparseable, so a failure here cannot happen
            // short of the options being built without it; skipping is still never "trust all".
            if (!ForwardedHeadersConfig.TryParseTrustedSource(entry, out var address, out var network))
                continue;

            if (network is { } subnet)
                options.KnownIPNetworks.Add(subnet);
            else if (address is not null)
                options.KnownProxies.Add(address);
        }
    }
}

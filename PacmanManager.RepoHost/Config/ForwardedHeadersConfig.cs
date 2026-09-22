using System.Net;

namespace PacmanManager.RepoHost.Config;

/// <summary>
/// The reverse proxies whose <c>X-Forwarded-*</c> headers are believed.
/// </summary>
/// <remarks>
/// An absent or empty <see cref="TrustedSources"/> keeps ASP.NET Core's defaults, which trust loopback
/// only. It never means "trust every caller": that would let any client choose the address that is
/// logged as its own.
/// </remarks>
public class ForwardedHeadersConfig
{
    /// <summary>
    /// Config section to find this object in.
    /// </summary>
    public const string Section = "ForwardedHeaders";

    /// <summary>
    /// The trusted proxies. Each entry is a single IPv4 or IPv6 address, or a subnet in CIDR notation.
    /// </summary>
    public List<string> TrustedSources { get; set; } = [];

    /// <summary>
    /// Parses one <see cref="TrustedSources"/> entry. An entry containing <c>/</c> is a subnet;
    /// anything else is a single address.
    /// </summary>
    /// <param name="entry">The configured entry.</param>
    /// <param name="address">The address, when the entry is a single address; otherwise <c>null</c>.</param>
    /// <param name="network">The subnet, when the entry is in CIDR notation; otherwise <c>null</c>.</param>
    /// <returns>
    /// <c>true</c> if the entry parsed, in which case exactly one of <paramref name="address"/> and
    /// <paramref name="network"/> is set.
    /// </returns>
    public static bool TryParseTrustedSource(string? entry, out IPAddress? address, out IPNetwork? network)
    {
        address = null;
        network = null;
        if (string.IsNullOrWhiteSpace(entry))
            return false;

        var trimmed = entry.Trim();
        if (trimmed.Contains('/'))
        {
            if (!IPNetwork.TryParse(trimmed, out var parsedNetwork))
                return false;
            network = parsedNetwork;
            return true;
        }

        return IPAddress.TryParse(trimmed, out address);
    }
}

using Microsoft.Extensions.Options;

namespace PacmanManager.RepoHost.Config;

/// <summary>
/// Fails startup on a <see cref="ForwardedHeadersConfig.TrustedSources"/> entry that is neither an
/// address nor a CIDR subnet, naming the entry. A mistyped proxy must not quietly become a proxy that
/// is not trusted.
/// </summary>
public class ValidateForwardedHeadersConfig : IValidateOptions<ForwardedHeadersConfig>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ForwardedHeadersConfig options)
    {
        var failures = options.TrustedSources
            .Where(entry => !ForwardedHeadersConfig.TryParseTrustedSource(entry, out _, out _))
            .Select(entry =>
                $"{ForwardedHeadersConfig.Section}:{nameof(ForwardedHeadersConfig.TrustedSources)} entry '{entry}' " +
                "is neither an IP address nor a subnet in CIDR notation.")
            .ToList();

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

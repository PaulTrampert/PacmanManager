using Microsoft.Extensions.Options;

namespace PacmanManager.RepoHost.Config;

/// <summary>
/// Fails startup on a <see cref="PublicUrlConfig.PublicUrl"/> that is set but is not an absolute
/// <c>http</c> or <c>https</c> URL, naming the value. A mistyped address must not quietly become a
/// <c>Server</c> line no <c>pacman</c> client can use.
/// </summary>
public class ValidatePublicUrlConfig : IValidateOptions<PublicUrlConfig>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, PublicUrlConfig options)
    {
        if (!options.IsConfigured || PublicUrlConfig.IsValidPublicUrl(options.PublicUrl))
            return ValidateOptionsResult.Success;

        return ValidateOptionsResult.Fail(
            $"{nameof(PublicUrlConfig.PublicUrl)} '{options.PublicUrl}' is not an absolute http or https URL.");
    }
}

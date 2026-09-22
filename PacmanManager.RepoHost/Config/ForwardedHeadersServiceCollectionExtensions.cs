using Microsoft.Extensions.Options;

namespace PacmanManager.RepoHost.Config;

/// <summary>
/// Registers the trusted forwarded-header sources.
/// </summary>
public static class ForwardedHeadersServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="ForwardedHeadersConfig"/> from its configuration section, validates it on
    /// startup, and builds <see cref="Microsoft.AspNetCore.Builder.ForwardedHeadersOptions"/> from it
    /// for <c>UseForwardedHeaders</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root holding the section.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddTrustedForwardedHeaders(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ForwardedHeadersConfig>()
            .Bind(configuration.GetSection(ForwardedHeadersConfig.Section))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ForwardedHeadersConfig>,
            ValidateForwardedHeadersConfig>();
        services.ConfigureOptions<ConfigureForwardedHeadersOptions>();
        return services;
    }
}

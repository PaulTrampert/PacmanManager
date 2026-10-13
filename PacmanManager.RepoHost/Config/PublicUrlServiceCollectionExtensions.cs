using Microsoft.Extensions.Options;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Config;

/// <summary>
/// Registers the <c>PublicUrl</c> setting and the <see cref="IServerUrlProvider"/> built from it.
/// </summary>
public static class PublicUrlServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="PublicUrlConfig"/> from the top-level <c>PublicUrl</c> key, validates it on
    /// startup, and registers <see cref="ServerUrlProvider"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root holding the key.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPublicUrl(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PublicUrlConfig>()
            .Bind(configuration)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<PublicUrlConfig>, ValidatePublicUrlConfig>();
        services.AddHttpContextAccessor();
        services.AddSingleton<IServerUrlProvider, ServerUrlProvider>();
        return services;
    }
}

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace PacmanManager.RepoHost.Config;

public class ConfigureJwtOptions(IOptions<AuthConfig> config, IHostEnvironment env)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly AuthConfig _config = config.Value;

    /// <inheritdoc />
    public void Configure(JwtBearerOptions options)
    {
        options.Authority = _config.Authority;
        options.Audience = _config.Audience;
        options.RequireHttpsMetadata = !env.IsDevelopment();
    }

    /// <inheritdoc />
    public void Configure(string? name, JwtBearerOptions options)
    {
        Configure(options);
    }
}

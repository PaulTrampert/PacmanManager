using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace PacmanManager.RepoHost.Config;

public class ConfigureJwtOptions(IOptions<AuthConfig> authConfig, IHostEnvironment env)
    : ConfigureOptions<JwtBearerOptions>(opts =>
    {
        opts.Authority = authConfig.Value.Authority;
        opts.Audience = authConfig.Value.Audience;
        opts.RequireHttpsMetadata = !env.IsDevelopment();
    });

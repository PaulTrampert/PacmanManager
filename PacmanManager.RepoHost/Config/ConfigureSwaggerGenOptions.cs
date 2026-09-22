using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using PacmanManager.RepoHost.Authentication;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PacmanManager.RepoHost.Config;

/// <summary>
/// Configuration options for SwaggerGen.
/// </summary>
public class ConfigureSwaggerGenOptions(
    IApiVersionDescriptionProvider provider,
    IOptions<SwaggerConfig> swaggerConfig)
    : IConfigureOptions<SwaggerGenOptions>
{
    private readonly SwaggerConfig _swaggerConfig = swaggerConfig.Value;

    /// <summary>
    /// The scopes the OpenId security requirement lists: the standard OIDC ones, the API's audience
    /// scope, and every value of this API's <c>scope</c> grammar.
    /// </summary>
    internal static IReadOnlyList<string> RequestedScopes { get; } =
        ["openid", "profile", "email", "roles", ScopeValues.Audience, ..ScopeValues.All];

    /// <inheritdoc />
    public void Configure(SwaggerGenOptions opts)
    {
        foreach (var description in provider.ApiVersionDescriptions.Where(desc =>
                     desc.ApiVersion != ApiVersion.Neutral))
            opts.SwaggerDoc(description.GroupName, CreateVersionInfo(description));

        var scheme = new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Name = "Authorization",
            Scheme = "Bearer",
            Type = SecuritySchemeType.OpenIdConnect,
            OpenIdConnectUrl = new Uri(_swaggerConfig.OpenIdConnectUrl)
        };
        opts.AddSecurityDefinition("OpenId", scheme);
        opts.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecuritySchemeReference("OpenId",  doc),
                RequestedScopes.ToList()
            }
        });
        var fileName = typeof(Program).Assembly.GetName().Name + ".xml";
        var filePath = Path.Combine(AppContext.BaseDirectory, fileName);
        opts.IncludeXmlComments(filePath);
        opts.SupportNonNullableReferenceTypes();
        opts.ParameterFilter<SortDirectionDefaultParameterFilter>();

        opts.MapType<DateOnly>(() => new OpenApiSchema
        {
            Format = "date",
            Type = JsonSchemaType.String
        });
    }

    private OpenApiInfo CreateVersionInfo(ApiVersionDescription desc)
    {
        var info = new OpenApiInfo
        {
            Title = "PacmanManager RepoHost Api",
            Version = desc.ApiVersion.ToString()
        };

        if (desc.IsDeprecated) info.Description += " DEPRECATED";

        return info;
    }
}
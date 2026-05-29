namespace PacmanManager.RepoHost.Config;

/// <summary>
/// Config for swagger.
/// </summary>
public record SwaggerConfig
{
    /// <summary>
    /// The config section name for this object.
    /// </summary>
    public const string Section = "Swagger";

    /// <summary>
    /// True if swagger ui should be enabled.
    /// </summary>
    public bool EnableSwaggerUi { get; init; }
    
    /// <summary>
    /// The client id for swagger ui.
    /// </summary>
    public string SwaggerClientId { get; init; } = "";
    
    /// <summary>
    /// The url for the openid connect server to auth against.
    /// </summary>
    public string OpenIdConnectUrl { get; init; } = "";
};
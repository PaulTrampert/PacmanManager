using PacmanManager.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Asp.Versioning.Conventions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.Packages;
using PacmanManager.Packages.Infrastructure;
using PacmanManager.Packages.LibAlpm;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Config;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;
using PTrampert.SimplePatch;
using Serilog;

// Configure Serilog
var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

try
{
    builder.Host.UseSerilog();

    #region Services

// Configure PacmanConfigSettings - always use DATA_DIR environment variable
    builder.Services.Configure<PacmanConfigSettings>(options => { options.DataDir = EnvironmentVariables.DataDir; });
    builder.Services.Configure<AuthConfig>(builder.Configuration.GetSection(AuthConfig.Section));
    builder.Services.Configure<SwaggerConfig>(builder.Configuration.GetSection(SwaggerConfig.Section));
    builder.Services.Configure<PackagePublishingConfig>(
        builder.Configuration.GetSection(PackagePublishingConfig.Section));
    builder.Services.Configure<AccessTokenConfig>(builder.Configuration.GetSection(AccessTokenConfig.Section));
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.ConfigureOptions<ConfigureJwtOptions>();
    builder.Services.AddTrustedForwardedHeaders(builder.Configuration);
    builder.Services.AddPublicUrl(builder.Configuration);

// Register CLI tool runner and logger
    builder.Services.AddCliToolRunner()
        .AddCliOutputLogger();

// The pacman configuration, libalpm, the package file tree, the access policies and the package
// publisher: everything a host that changes repositories shares with every other one.
    builder.Services.AddPacmanPackages();

// Every mutation of a repository database is serialized per repository, across every process, by a
// Postgres advisory lock. It owns the data source its lock connections come from, so it is a
// singleton, built only when first needed so that generating the OpenAPI document needs no database.
    builder.Services.AddSingleton<IRepositoryDatabaseLock>(_ => new PostgresRepositoryDatabaseLock(
        builder.Configuration.GetConnectionString("pacmanmanager")
        ?? throw new InvalidOperationException("ConnectionStrings:pacmanmanager is not configured.")));
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IRepositoryService, RepositoryService>();
    builder.Services.AddScoped<IPackageService, PackageService>();
    builder.Services.AddScoped<IPacmanRepoService, PacmanRepoService>();
    builder.Services.AddScoped<IUserManagementService, UserManagementService>();
    builder.Services.AddScoped<IAccessTokenService, AccessTokenService>();

// Services enforce authorization against whatever actor the host supplies. This is the web host,
// so the actor comes from the authenticated principal; a CLI tool or background job would register
// a FixedActorAccessor instead. Nothing is registered by default, so a host that forgets to choose
// fails to start rather than running as an unidentified caller.
    builder.Services.AddScoped<IActorAccessor, HttpContextActorAccessor>();
    builder.Services.AddSingleton<AccessTokenAccessPolicy>();
    builder.Services.AddSingleton<UserAccessPolicy>();

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<AuthorizationExceptionHandler>();
    builder.Services.AddExceptionHandler<PackagePublishExceptionHandler>();

    builder.Services.AddApiVersioning(opts =>
        {
            opts.ReportApiVersions = true;
            opts.DefaultApiVersion = ApiVersion.Default;
        })
        .AddMvc(opts => { opts.Conventions.Add(new VersionByNamespaceConvention()); })
        .AddApiExplorer(opts =>
        {
            opts.GroupNameFormat = "'v'V";
            opts.SubstituteApiVersionInUrl = true;
        });
    builder.Services.ConfigureOptions<ConfigureSwaggerGenOptions>();
    builder.Services.AddSwaggerGen(c =>
    {
        c.DescribeAllParametersInCamelCase();
    });

    builder.Services.AddControllers()
        .AddJsonOptions(opt =>
        {
            opt.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            opt.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            opt.JsonSerializerOptions.AddSimplePatchConverters();
        });

    JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();
    // The Authorization header's prefix picks the handler: Basic for an access token, Bearer for
    // everything else. Swagger is deliberately left describing Bearer only.
    builder.Services.AddPacmanAuthentication();
    builder.Services.AddScoped<IClaimsTransformation, ClaimsTransformer>();
    builder.Services.AddAuthorization();

    builder.Services.AddDbContext<PacmanManagerDbContext>(opts =>
    {
        opts.UseNpgsql(builder.Configuration.GetConnectionString("pacmanmanager"));
    });

    // Startup work that needs DATA_DIR runs in hosted services, never after builder.Build(): the
    // Swashbuckle CLI runs this whole file to generate the OpenAPI document but strips hosted
    // services, so generation needs no data directory.
    builder.Services.AddHostedService<PacmanDirectoriesInitializer>();

    #endregion

    #region Request Pipeline

    var app = builder.Build();
    // First, so that request logging, authentication and everything after them see the client's
    // address rather than the reverse proxy's.
    app.UseForwardedHeaders();
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();

    // Configure the HTTP request pipeline.
    var swaggerConfig = app.Services.GetRequiredService<IOptions<SwaggerConfig>>().Value;
    if (app.Environment.IsDevelopment() || swaggerConfig.EnableSwaggerUi)
    {
        app.UseSwagger();
        app.UseSwaggerUI(opts =>
        {
            foreach (var description in app.DescribeApiVersions().Where(desc => desc.ApiVersion != ApiVersion.Neutral))
                opts.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json",
                    description.GroupName.ToUpperInvariant());

            opts.OAuthClientId(swaggerConfig.SwaggerClientId ?? "pacman-manager-swagger");
            opts.OAuthUsePkce();
            opts.EnablePersistAuthorization();
        });
    }

    app.UseAuthentication();
    // Before authorization, so that an [AllowAnonymous] route cannot swallow a refused Basic
    // credential and answer as if none had been offered.
    app.UseRejectInvalidBasicCredentials();
    app.UseAuthorization();
    app.MapControllers();

    #endregion

    Log.Information("Starting PacmanManager.RepoHost");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Make Program class accessible to integration tests
public partial class Program
{
}
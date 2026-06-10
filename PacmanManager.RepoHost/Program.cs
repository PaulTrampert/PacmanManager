using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Asp.Versioning.Conventions;
using LibAlpmSharp;
using LibAlpmSharp.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.RepoHost;
using PacmanManager.RepoHost.Config;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.RepoHost.Services;
using PacmanManager.RepoHost.Startup.LibAlpm;
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
    builder.Services.ConfigureOptions<ConfigureJwtOptions>();

// Register config generator and serializer
    builder.Services.AddSingleton<IPacmanConfigSerializer, PacmanConfigSerializer>();
    builder.Services.AddSingleton<IPacmanConfigReader, PacmanConfigReader>();
    builder.Services.AddSingleton<IPacmanConfigGenerator, PacmanConfigGenerator>();

// Register CLI tool runner and logger
    builder.Services.AddCliToolRunner()
        .AddCliOutputLogger();

    builder.Services.AddSingleton<IFileSystem, PhysicalFileSystem>();
    
    builder.Services.AddScoped<IRepositoryService, RepositoryService>();

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
    builder.Services.AddSwaggerGen(c => c.DescribeAllParametersInCamelCase());

    builder.Services.AddControllers()
        .AddJsonOptions(opt =>
        {
            opt.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            opt.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer();
    builder.Services.AddAuthorization();

    builder.Services.AddDbContext<PacmanManagerDbContext>(opts =>
    {
        opts.UseNpgsql(builder.Configuration.GetConnectionString("pacmanmanager"));
    });

// Generate pacman config in-memory and parse it
    builder.Services.AddTransient<PacmanConfig>(p =>
    {
        var configGenerator = p.GetRequiredService<IPacmanConfigGenerator>();
        var configSettings = p.GetRequiredService<IOptions<PacmanConfigSettings>>().Value;
        var configContent = configGenerator.GenerateConfigContent();
        var configReader = new PacmanConfigReader(p.GetRequiredService<ILogger<PacmanConfigReader>>());
        return configReader.ReadConfigFromString(configContent, configSettings.DataDir);
    });

    builder.Services.AddScoped<ILibAlpm>(p =>
        LibAlpm.FromConfig(p.GetRequiredService<PacmanConfig>()));

    #endregion

    #region Request Pipeline

    var app = builder.Build();
    app.UseSerilogRequestLogging();
    
    var pacmanConfig = app.Services.GetRequiredService<PacmanConfig>();
    Directory.CreateDirectory(Path.Combine(pacmanConfig.DBPath, "sync"));
    Directory.CreateDirectory(Path.Combine(pacmanConfig.DBPath, "local"));

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
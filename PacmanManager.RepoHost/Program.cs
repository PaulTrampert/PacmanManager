using System.Text.Json;
using System.Text.Json.Serialization;
using LibAlpmSharp;
using LibAlpmSharp.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.RepoHost;
using PacmanManager.RepoHost.Config;
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

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();

// Configure PacmanConfigSettings - always use DATA_DIR environment variable
    builder.Services.Configure<PacmanConfigSettings>(options => { options.DataDir = EnvironmentVariables.DataDir; });
    builder.Services.Configure<AuthConfig>(builder.Configuration.GetSection(AuthConfig.Section));
    builder.Services.ConfigureOptions<ConfigureJwtOptions>();

// Register config generator and serializer
    builder.Services.AddSingleton<IPacmanConfigSerializer, PacmanConfigSerializer>();
    builder.Services.AddSingleton<IPacmanConfigReader, PacmanConfigReader>();
    builder.Services.AddSingleton<IPacmanConfigGenerator, PacmanConfigGenerator>();

// Register CLI tool runner and logger
    builder.Services.AddCliToolRunner()
        .AddCliOutputLogger();

    builder.Services.AddControllers()
        .AddJsonOptions(opt =>
        {
            opt.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            opt.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

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
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
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
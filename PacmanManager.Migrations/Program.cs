// See https://aka.ms/new-console-template for more information

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PacmanManager.Entities;
using Serilog;

var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ApplicationName = "PacmanManager.Migrations",
    ContentRootPath = AppContext.BaseDirectory,
    Configuration = new ConfigurationManager()
});

builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Services.AddSerilog();

var connectionString = builder.Configuration.GetConnectionString("pacmanmanager");
var targetMigration = builder.Configuration.GetValue<string?>("TargetMigration");
var maxRetries = builder.Configuration.GetValue("MaxRetries", 5);
var retryDelaySeconds = builder.Configuration.GetValue("RetryDelaySeconds", 5);
builder.Services.AddDbContext<PacmanManagerDbContext>(opts =>
    {
        opts.UseNpgsql(
            connectionString, 
            b => b.MigrationsAssembly("PacmanManager.Migrations")
        );
    });
    
var app = builder.Build();
var success = false;
var attempt = 0;

var logger = app.Services.GetRequiredService<ILogger<Program>>();
while (!success && attempt < maxRetries)
{
    using var scope = app.Services.CreateScope();
    try
    {
        logger.LogInformation(
            "Migrating database to {TargetMigration} (Attempt {Attempt}/{MaxRetries})",
            targetMigration ?? "Latest", 
            attempt + 1, 
            maxRetries
        );
        var dbContext = scope.ServiceProvider.GetRequiredService<PacmanManagerDbContext>();
        dbContext.Database.Migrate(targetMigration);
        success = true;
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "An error occurred while migrating the database.");
        await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
    }

    attempt++;
}

if (!success)
{
    logger.LogCritical("Failed to migrate database after {MaxRetries} attempts. Exiting with failure.", maxRetries);
    Environment.Exit(1);
}
else
{
    logger.LogInformation("Migration completed successfully.");
}
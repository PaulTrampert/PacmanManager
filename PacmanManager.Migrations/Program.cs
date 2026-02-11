// See https://aka.ms/new-console-template for more information

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PacmanManager.Entities;

var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ApplicationName = "PacmanManager.Migrations",
    ContentRootPath = AppContext.BaseDirectory,
});

var connectionString = builder.Configuration.GetConnectionString("pacmanmanager");
var targetMigration = builder.Configuration.GetSection("TargetMigration").Value;
builder.Services.AddDbContext<PacmanManagerDbContext>(opts =>
    {
        opts.UseNpgsql(
            connectionString, 
            b => b.MigrationsAssembly("PacmanManager.Migrations")
        );
    });
    
var app = builder.Build();

using var scope = app.Services.CreateScope();
scope.ServiceProvider.GetRequiredService<PacmanManagerDbContext>().Database.Migrate(targetMigration);

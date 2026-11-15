using LibAlpmSharp;
using LibAlpmSharp.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacmanManager.Packages.Authorization;
using PacmanManager.Packages.Infrastructure;
using PacmanManager.Packages.LibAlpm;

namespace PacmanManager.Packages;

/// <summary>
/// Registers what publishing and removing packages needs, so that every host that changes a
/// repository does it with the same services.
/// </summary>
public static class PackagesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the pacman configuration and libalpm, the file system and path resolver, both
    /// access policies, the package reader and the <see cref="IPackagePublisher"/>.
    /// </summary>
    /// <remarks>
    /// The host still chooses, and registers itself, the <see cref="PacmanConfigSettings"/>, the
    /// <see cref="IRepositoryDatabaseLock"/>, the CLI tool runner, the
    /// <c>PacmanManagerDbContext</c> and the actor accessor, and whether to run
    /// <see cref="PacmanDirectoriesInitializer"/>.
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddPacmanPackages(this IServiceCollection services)
    {
        services.AddSingleton<IPacmanConfigSerializer, PacmanConfigSerializer>();
        services.AddSingleton<IPacmanConfigReader, PacmanConfigReader>();
        services.AddSingleton<IPacmanConfigGenerator, PacmanConfigGenerator>();

        // Generate pacman config in-memory and parse it
        services.AddTransient<PacmanConfig>(p =>
        {
            var configGenerator = p.GetRequiredService<IPacmanConfigGenerator>();
            var configSettings = p.GetRequiredService<IOptions<PacmanConfigSettings>>().Value;
            var configContent = configGenerator.GenerateConfigContent();
            var configReader = new PacmanConfigReader(p.GetRequiredService<ILogger<PacmanConfigReader>>());
            return configReader.ReadConfigFromString(configContent, configSettings.DataDir);
        });

        services.AddScoped<ILibAlpm>(p =>
            LibAlpmSharp.LibAlpm.FromConfig(p.GetRequiredService<PacmanConfig>()));

        // Lazy<T> is not something Microsoft.Extensions.DependencyInjection resolves on its own, so
        // the deferred handle consumers ask for is registered explicitly. It is scoped to match
        // ILibAlpm: the value it produces is the scope's own instance and is disposed with the
        // scope, and forcing it is what finally pays for regenerating the pacman config and calling
        // alpm_initialize. A consumer that never reads a package file therefore never pays at all.
        services.AddScoped(p => new Lazy<ILibAlpm>(p.GetRequiredService<ILibAlpm>));

        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IPackagePathResolver, PackagePathResolver>();

        services.AddSingleton<RepositoryAccessPolicy>();
        services.AddSingleton<PackageAccessPolicy>();

        services.AddScoped<IPackageFileReader, PackageFileReader>();
        services.AddScoped<IPackagePublisher, PackagePublisher>();

        return services;
    }
}

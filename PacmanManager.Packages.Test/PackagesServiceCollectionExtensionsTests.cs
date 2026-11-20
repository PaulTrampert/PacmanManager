using LibAlpmSharp;
using LibAlpmSharp.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.Packages.Authorization;
using PacmanManager.Packages.Infrastructure;
using PacmanManager.Packages.LibAlpm;

namespace PacmanManager.Packages.Test;

/// <summary>
/// Tests for <see cref="PackagesServiceCollectionExtensions.AddPacmanPackages"/>.
/// </summary>
[TestFixture]
public class PackagesServiceCollectionExtensionsTests
{
    private const string DataDir = "/tmp/pacmanmanager-packages-di";

    private ServiceProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<PacmanConfigSettings>(o => o.DataDir = DataDir);
        services.AddDbContext<PacmanManagerDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton(Mock.Of<ICliToolRunner>());
        services.AddSingleton<IRepositoryDatabaseLock, NoOpRepositoryDatabaseLock>();

        services.AddPacmanPackages();

        // Registered after, so it replaces the real factory: resolving libalpm now fails loudly.
        services.AddScoped<ILibAlpm>(_ =>
            throw new InvalidOperationException("Resolving the publisher must not initialize libalpm."));

        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [TearDown]
    public void TearDown() => _provider.Dispose();

    [Test]
    public void ThePublisher_Resolves_WithoutInitializingLibAlpm()
    {
        // Arrange
        using var scope = _provider.CreateScope();

        // Act
        var publisher = scope.ServiceProvider.GetRequiredService<IPackagePublisher>();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(publisher, Is.InstanceOf<PackagePublisher>());
            Assert.That(scope.ServiceProvider.GetRequiredService<Lazy<ILibAlpm>>().IsValueCreated, Is.False);
        });
    }

    [Test]
    public void TheAccessPolicies_AreSingletons()
    {
        // Act
        var repositoryPolicy = _provider.GetRequiredService<RepositoryAccessPolicy>();
        var packagePolicy = _provider.GetRequiredService<PackageAccessPolicy>();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_provider.GetRequiredService<RepositoryAccessPolicy>(), Is.SameAs(repositoryPolicy));
            Assert.That(_provider.GetRequiredService<PackageAccessPolicy>(), Is.SameAs(packagePolicy));
        });
    }

    [Test]
    public void ThePackageTree_IsLaidOutUnderTheConfiguredDataDirectory()
    {
        // Act
        var pathResolver = _provider.GetRequiredService<IPackagePathResolver>();
        var repositoryId = Guid.NewGuid();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_provider.GetRequiredService<IFileSystem>(), Is.InstanceOf<PhysicalFileSystem>());
            Assert.That(pathResolver.GetRepositoryDirectory(repositoryId),
                Is.EqualTo(Path.Combine(DataDir, "repositories", repositoryId.ToString())));
        });
    }

    [Test]
    public void ThePacmanConfiguration_KeepsLibalpmsFilesUnderTheConfiguredDataDirectory()
    {
        // Act
        var config = _provider.GetRequiredService<PacmanConfig>();

        // Assert
        Assert.That(config.DBPath, Is.EqualTo(Path.Combine(DataDir, "libalpm")));
    }
}

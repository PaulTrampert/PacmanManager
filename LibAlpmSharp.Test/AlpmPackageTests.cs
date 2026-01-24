using System;
using NUnit.Framework;

namespace LibAlpmSharp.Test;

[TestFixture]
public class AlpmPackageTests
{
    private const string TestPackageName = "bash";

    [Test]
    public void Name_ReturnsPackageName()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            AlpmPackage? pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            Assert.That(pkg!.Name, Is.EqualTo(TestPackageName));
            
            TestContext.WriteLine($"Package name: {pkg.Name}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void Version_ReturnsPackageVersion()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            AlpmPackage? pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            Assert.That(pkg!.Version, Is.Not.Null.And.Not.Empty);
            
            TestContext.WriteLine($"Package version: {pkg.Version}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void Description_ReturnsPackageDescription()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            AlpmPackage? pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            Assert.That(pkg!.Description, Is.Not.Null.And.Not.Empty);
            
            TestContext.WriteLine($"Package description: {pkg.Description}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void ToString_ReturnsNameAndVersion()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            AlpmPackage? pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            string pkgString = pkg!.ToString();
            Assert.That(pkgString, Is.Not.Null.And.Not.Empty);
            Assert.That(pkgString, Does.Contain(pkg.Name));
            Assert.That(pkgString, Does.Contain(pkg.Version));
            
            TestContext.WriteLine($"Package ToString: {pkgString}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetBase_ReturnsPackageBase()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            AlpmPackage? pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            string pkgBase = pkg!.GetBase();
            Assert.That(pkgBase, Is.Not.Null);
            
            TestContext.WriteLine($"Package base: {pkgBase}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetUrl_ReturnsPackageUrl()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            AlpmPackage? pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            string url = pkg!.GetUrl();
            Assert.That(url, Is.Not.Null);
            
            TestContext.WriteLine($"Package URL: {url}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetArchitecture_ReturnsPackageArchitecture()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            AlpmPackage? pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            string arch = pkg!.GetArchitecture();
            Assert.That(arch, Is.Not.Null.And.Not.Empty);
            
            TestContext.WriteLine($"Package architecture: {arch}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetPackager_ReturnsPackagerName()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            AlpmPackage? pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            string packager = pkg!.GetPackager();
            Assert.That(packager, Is.Not.Null);
            
            TestContext.WriteLine($"Package packager: {packager}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetInstalledSize_ReturnsPositiveSize()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            AlpmPackage? pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            long installedSize = pkg!.GetInstalledSize();
            Assert.That(installedSize, Is.GreaterThan(0), "Installed size should be positive");
            
            TestContext.WriteLine($"Package installed size: {installedSize} bytes ({installedSize / 1024.0:F2} KB)");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetDownloadSize_ReturnsSize()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            AlpmPackage? pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            long downloadSize = pkg!.GetDownloadSize();
            Assert.That(downloadSize, Is.GreaterThanOrEqualTo(0), "Download size should be non-negative");
            
            TestContext.WriteLine($"Package download size: {downloadSize} bytes ({downloadSize / 1024.0:F2} KB)");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetBuildDate_ReturnsValidDate()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            AlpmPackage? pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            DateTimeOffset buildDate = pkg!.GetBuildDate();
            Assert.That(buildDate, Is.Not.EqualTo(DateTimeOffset.MinValue));
            Assert.That(buildDate, Is.LessThanOrEqualTo(DateTimeOffset.UtcNow), "Build date should not be in the future");
            
            TestContext.WriteLine($"Package build date: {buildDate:yyyy-MM-dd HH:mm:ss}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetInstallDate_ReturnsValidDate()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            AlpmPackage? pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            DateTimeOffset? installDate = pkg!.GetInstallDate();
            
            // For a local database package, it should have an install date
            Assert.That(installDate, Is.Not.Null);
            Assert.That(installDate!.Value, Is.LessThanOrEqualTo(DateTimeOffset.UtcNow), "Install date should not be in the future");
            
            TestContext.WriteLine($"Package install date: {installDate.Value:yyyy-MM-dd HH:mm:ss}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetInstallDate_BeforeGetBuildDate()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            AlpmPackage? pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            DateTimeOffset buildDate = pkg!.GetBuildDate();
            DateTimeOffset? installDate = pkg.GetInstallDate();
            
            Assert.That(installDate, Is.Not.Null);
            Assert.That(installDate!.Value, Is.GreaterThanOrEqualTo(buildDate), 
                "Install date should be equal to or after build date");
            
            TestContext.WriteLine($"Build date: {buildDate:yyyy-MM-dd HH:mm:ss}");
            TestContext.WriteLine($"Install date: {installDate.Value:yyyy-MM-dd HH:mm:ss}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void MultiplePropertyAccess_ReturnsConsistentValues()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            AlpmPackage? pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            
            // Access properties multiple times to ensure consistency
            string name1 = pkg!.Name;
            string name2 = pkg.Name;
            Assert.That(name1, Is.EqualTo(name2));
            
            string version1 = pkg.Version;
            string version2 = pkg.Version;
            Assert.That(version1, Is.EqualTo(version2));
            
            string desc1 = pkg.Description;
            string desc2 = pkg.Description;
            Assert.That(desc1, Is.EqualTo(desc2));
            
            TestContext.WriteLine("All properties return consistent values on multiple accesses");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void AllMethods_DoNotThrowExceptions()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            AlpmPackage? pkg = localDb.GetPackage(TestPackageName);
            
            Assert.That(pkg, Is.Not.Null);
            
            // Act & Assert - call all methods to ensure they don't throw exceptions
            Assert.DoesNotThrow(() => { var _ = pkg!.Name; });
            Assert.DoesNotThrow(() => { var _ = pkg!.Version; });
            Assert.DoesNotThrow(() => { var _ = pkg!.Description; });
            Assert.DoesNotThrow(() => { var _ = pkg!.ToString(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetBase(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetUrl(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetArchitecture(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetPackager(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetInstalledSize(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetDownloadSize(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetBuildDate(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetInstallDate(); });
            
            TestContext.WriteLine("All methods executed without throwing exceptions");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }
}

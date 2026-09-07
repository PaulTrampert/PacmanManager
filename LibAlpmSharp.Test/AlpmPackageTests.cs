using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PacmanManager.TestUtils;

namespace LibAlpmSharp.Test;

[TestFixture]
public class AlpmPackageTests
{
    private const string TestPackageName = "pacman";

    [Test]
    public void Name_ReturnsPackageName()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            var pkg = localDb.GetPackage(TestPackageName);
            
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
            var pkg = localDb.GetPackage(TestPackageName);
            
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
            var pkg = localDb.GetPackage(TestPackageName);
            
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
            var pkg = localDb.GetPackage(TestPackageName);
            
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
            var pkg = localDb.GetPackage(TestPackageName);
            
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
            var pkg = localDb.GetPackage(TestPackageName);
            
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
            var pkg = localDb.GetPackage(TestPackageName);
            
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
            var pkg = localDb.GetPackage(TestPackageName);
            
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
            var pkg = localDb.GetPackage(TestPackageName);
            
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
            var pkg = localDb.GetPackage(TestPackageName);
            
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
            var pkg = localDb.GetPackage(TestPackageName);
            
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
            var pkg = localDb.GetPackage(TestPackageName);
            
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
            var pkg = localDb.GetPackage(TestPackageName);
            
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
            var pkg = localDb.GetPackage(TestPackageName);
            
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
            var pkg = localDb.GetPackage(TestPackageName);
            
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
            Assert.DoesNotThrow(() => { var _ = pkg!.GetFileName(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetSha256Sum(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetMd5Sum(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetLicenses(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetGroups(); });
            
            TestContext.WriteLine("All methods executed without throwing exceptions");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetDependencies_ReturnsListOfDependencies()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            var pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            var dependencies = pkg!.GetDependencies();
            Assert.That(dependencies, Is.Not.Null);
            
            TestContext.WriteLine($"Package {pkg.Name} has {dependencies.Count} dependencies:");
            foreach (var dep in dependencies)
            {
                TestContext.WriteLine($"  - {dep}");
                Assert.That(dep.Name, Is.Not.Null.And.Not.Empty, "Dependency name should not be null or empty");
            }
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetDependencies_DependencyHasValidProperties()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            var pkg = localDb.GetPackage(TestPackageName);
            
            Assert.That(pkg, Is.Not.Null);
            var dependencies = pkg!.GetDependencies();
            
            // Act & Assert
            if (dependencies.Count > 0)
            {
                var firstDep = dependencies[0];
                Assert.That(firstDep.Name, Is.Not.Null.And.Not.Empty);
                Assert.That(firstDep.ToString(), Does.Contain(firstDep.Name));
                
                TestContext.WriteLine($"First dependency: {firstDep}");
                TestContext.WriteLine($"  Name: {firstDep.Name}");
                TestContext.WriteLine($"  Version: {firstDep.Version ?? "(any)"}");
                TestContext.WriteLine($"  Modifier: {firstDep.Modifier}");
                TestContext.WriteLine($"  Description: {firstDep.Description ?? "(none)"}");
            }
            else
            {
                TestContext.WriteLine($"Package {pkg.Name} has no dependencies");
            }
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetOptionalDependencies_ReturnsListOfOptionalDependencies()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            var pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            var optDeps = pkg!.GetOptionalDependencies();
            Assert.That(optDeps, Is.Not.Null);
            
            TestContext.WriteLine($"Package {pkg.Name} has {optDeps.Count} optional dependencies:");
            foreach (var dep in optDeps)
            {
                TestContext.WriteLine($"  - {dep}");
                if (dep.Description != null)
                {
                    TestContext.WriteLine($"    ({dep.Description})");
                }
            }
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetRequiredBy_ReturnsListOfPackages()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            var pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            var requiredBy = pkg!.GetRequiredBy();
            Assert.That(requiredBy, Is.Not.Null);
            
            TestContext.WriteLine($"Package {pkg.Name} is required by {requiredBy.Count} package(s):");
            foreach (var pkgName in requiredBy)
            {
                TestContext.WriteLine($"  - {pkgName}");
                Assert.That(pkgName, Is.Not.Null.And.Not.Empty, "Package name should not be null or empty");
            }
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetOptionalFor_ReturnsListOfPackages()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            var pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            var optionalFor = pkg!.GetOptionalFor();
            Assert.That(optionalFor, Is.Not.Null);
            
            TestContext.WriteLine($"Package {pkg.Name} is optional for {optionalFor.Count} package(s):");
            foreach (var pkgName in optionalFor)
            {
                TestContext.WriteLine($"  - {pkgName}");
                Assert.That(pkgName, Is.Not.Null.And.Not.Empty, "Package name should not be null or empty");
            }
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetConflicts_ReturnsListOfConflicts()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            var pkg = localDb.GetPackage(TestPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            var conflicts = pkg!.GetConflicts();
            Assert.That(conflicts, Is.Not.Null);
            
            TestContext.WriteLine($"Package {pkg.Name} has {conflicts.Count} conflict(s):");
            foreach (var conflict in conflicts)
            {
                TestContext.WriteLine($"  - {conflict}");
                Assert.That(conflict.Name, Is.Not.Null.And.Not.Empty, "Conflict name should not be null or empty");
            }
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetDependencies_CanBeCalledMultipleTimes()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            var pkg = localDb.GetPackage(TestPackageName);
            
            Assert.That(pkg, Is.Not.Null);
            
            // Act
            var deps1 = pkg!.GetDependencies();
            var deps2 = pkg.GetDependencies();
            
            // Assert
            Assert.That(deps1.Count, Is.EqualTo(deps2.Count), "Multiple calls should return same count");
            
            TestContext.WriteLine($"GetDependencies() can be called multiple times consistently");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void AllNewMethods_DoNotThrowExceptions()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            var pkg = localDb.GetPackage(TestPackageName);
            
            Assert.That(pkg, Is.Not.Null);
            
            // Act & Assert - call all new methods to ensure they don't throw exceptions
            Assert.DoesNotThrow(() => { var _ = pkg!.GetDependencies(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetOptionalDependencies(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetRequiredBy(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetOptionalFor(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetConflicts(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetProvides(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetReplaces(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetMakeDepends(); });
            Assert.DoesNotThrow(() => { var _ = pkg!.GetCheckDepends(); });
            
            TestContext.WriteLine("All new dependency methods executed without throwing exceptions");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetLicenses_ReturnsListOfLicenses()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();

            // Act
            var pkg = localDb.GetPackage(TestPackageName);

            // Assert
            Assert.That(pkg, Is.Not.Null);
            var licenses = pkg!.GetLicenses();
            Assert.That(licenses, Is.Not.Null);
            Assert.That(licenses, Has.All.Not.Empty);

            TestContext.WriteLine($"Package {pkg.Name} licenses: {string.Join(", ", licenses)}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetGroups_ReturnsListOfGroups()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();

            // Act
            var pkg = localDb.GetPackage(TestPackageName);

            // Assert
            Assert.That(pkg, Is.Not.Null);
            var groups = pkg!.GetGroups();
            Assert.That(groups, Is.Not.Null);
            Assert.That(groups, Has.All.Not.Empty);

            TestContext.WriteLine($"Package {pkg.Name} groups: {string.Join(", ", groups)}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetProvides_ReturnsListOfProvisions()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();

            // Act
            var pkg = localDb.GetPackage(TestPackageName);

            // Assert
            Assert.That(pkg, Is.Not.Null);
            var provides = pkg!.GetProvides();
            Assert.That(provides, Is.Not.Null);
            Assert.That(provides.Select(p => p.Name), Has.All.Not.Empty);

            TestContext.WriteLine($"Package {pkg.Name} provides: {string.Join(", ", provides)}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetReplaces_ReturnsListOfReplacedPackages()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();

            // Act
            var pkg = localDb.GetPackage(TestPackageName);

            // Assert
            Assert.That(pkg, Is.Not.Null);
            var replaces = pkg!.GetReplaces();
            Assert.That(replaces, Is.Not.Null);
            Assert.That(replaces.Select(r => r.Name), Has.All.Not.Empty);

            TestContext.WriteLine($"Package {pkg.Name} replaces: {string.Join(", ", replaces)}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetMakeDepends_ReturnsListOfMakeDependencies()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();

            // Act
            var pkg = localDb.GetPackage(TestPackageName);

            // Assert - the local database does not record build-time dependencies, so the list is
            // expected to be empty here; the fixture package below is what pins real values.
            Assert.That(pkg, Is.Not.Null);
            var makeDepends = pkg!.GetMakeDepends();
            Assert.That(makeDepends, Is.Not.Null);
            Assert.That(makeDepends.Select(d => d.Name), Has.All.Not.Empty);

            TestContext.WriteLine($"Package {pkg.Name} make depends: {string.Join(", ", makeDepends)}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetCheckDepends_ReturnsListOfCheckDependencies()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();

            // Act
            var pkg = localDb.GetPackage(TestPackageName);

            // Assert - as with make dependencies, the local database records none.
            Assert.That(pkg, Is.Not.Null);
            var checkDepends = pkg!.GetCheckDepends();
            Assert.That(checkDepends, Is.Not.Null);
            Assert.That(checkDepends.Select(d => d.Name), Has.All.Not.Empty);

            TestContext.WriteLine($"Package {pkg.Name} check depends: {string.Join(", ", checkDepends)}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    /// <summary>
    /// Covers the members of <see cref="IPackage"/> against the committed fixture package rather
    /// than against whatever the host happens to have installed, which is the only way to assert
    /// exact values, and which is also the shape every package the packages API sees will have:
    /// loaded from a file, belonging to no database and installed nowhere.
    /// </summary>
    [TestFixture]
    public class FileLoadedPackage
    {
        private string _tempDirectory = null!;
        private LibAlpm _alpm = null!;
        private AlpmPackage _pkg = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"libalpmsharp-{Guid.NewGuid():N}");
            var root = Path.Combine(_tempDirectory, "root");
            var dbPath = Path.Combine(_tempDirectory, "db");
            System.IO.Directory.CreateDirectory(root);
            System.IO.Directory.CreateDirectory(dbPath);

            _alpm = LibAlpm.Initialize(root, dbPath);
            _pkg = (AlpmPackage)_alpm.LoadPackageFile(PackageFixtures.MinimalPackagePath);
        }

        [TearDown]
        public void TearDown()
        {
            _pkg.Dispose();
            _alpm.Dispose();

            if (System.IO.Directory.Exists(_tempDirectory))
                System.IO.Directory.Delete(_tempDirectory, recursive: true);
        }

        [Test]
        public void GetInstallDate_ForAPackageThatIsNotInstalled_ReturnsNull()
        {
            // A package read off disk has never been installed. libalpm reports a zero timestamp,
            // which must not be reported back as the Unix epoch.
            Assert.That(_pkg.GetInstallDate(), Is.Null);
        }

        [Test]
        public void GetSha256Sum_ForAFileLoadedPackage_ReturnsNull()
        {
            // libalpm fills this from a sync database entry and a file-loaded package has none.
            // If this ever starts returning a value, PackageService's own checksum computation
            // should be revisited rather than silently left in place.
            Assert.That(_pkg.GetSha256Sum(), Is.Null);
        }

        [Test]
        public void GetMd5Sum_ForAFileLoadedPackage_ReturnsNull()
        {
            // Same as the SHA256 sum: populated from a sync database entry, which there is none of.
            Assert.That(_pkg.GetMd5Sum(), Is.Null);
        }

        [Test]
        public void GetFileName_ForAFileLoadedPackage_ReturnsThePathItWasHandedRatherThanABasename()
        {
            // Pinned because it is a trap: the name says "file name" but the value is the whole
            // path given to alpm_pkg_load, so nothing may use it to name a stored file.
            Assert.That(_pkg.GetFileName(), Is.EqualTo(PackageFixtures.MinimalPackagePath));
        }

        [Test]
        public void GetLicenses_ReturnsTheDeclaredLicenses()
        {
            Assert.That(_pkg.GetLicenses(), Is.EqualTo(new[] { PackageFixtures.MinimalPackageLicense }));
        }

        [Test]
        public void GetGroups_ReturnsTheDeclaredGroups()
        {
            Assert.That(_pkg.GetGroups(), Is.EqualTo(new[] { PackageFixtures.MinimalPackageGroup }));
        }

        [Test]
        public void GetProvides_ReturnsTheDeclaredProvisions()
        {
            Assert.That(
                _pkg.GetProvides().Select(p => p.ToString()),
                Is.EqualTo(new[] { PackageFixtures.MinimalPackageProvides }));
        }

        [Test]
        public void GetReplaces_ReturnsTheDeclaredReplacements()
        {
            Assert.That(
                _pkg.GetReplaces().Select(r => r.ToString()),
                Is.EqualTo(new[] { PackageFixtures.MinimalPackageReplaces }));
        }

        [Test]
        public void GetMakeDepends_ReturnsTheDeclaredMakeDependencies()
        {
            Assert.That(
                _pkg.GetMakeDepends().Select(d => d.ToString()),
                Is.EqualTo(new[] { PackageFixtures.MinimalPackageMakeDepend }));
        }

        [Test]
        public void GetCheckDepends_ReturnsTheDeclaredCheckDependencies()
        {
            Assert.That(
                _pkg.GetCheckDepends().Select(d => d.ToString()),
                Is.EqualTo(new[] { PackageFixtures.MinimalPackageCheckDepend }));
        }

        [Test]
        public void GetConflicts_ReturnsTheDeclaredConflicts()
        {
            Assert.That(
                _pkg.GetConflicts().Select(c => c.ToString()),
                Is.EqualTo(new[] { PackageFixtures.MinimalPackageConflict }));
        }

        [Test]
        public void GetDependencies_ReturnsTheDeclaredDependencies()
        {
            Assert.That(
                _pkg.GetDependencies().Select(d => d.ToString()),
                Is.EqualTo(new[] { PackageFixtures.MinimalPackageDepend }));
        }

        [Test]
        public void GetOptionalDependencies_ReturnsTheDeclaredOptionalDependencies()
        {
            var optDepends = _pkg.GetOptionalDependencies();

            // pacman spells an optional dependency "name: reason", and AlpmDependency keeps the
            // reason in Description rather than in ToString().
            Assert.Multiple(() =>
            {
                Assert.That(optDepends.Select(d => d.Name), Is.EqualTo(new[] { "git" }));
                Assert.That(optDepends.Single().Description, Is.EqualTo("for the sync command"));
                Assert.That(
                    $"{optDepends.Single().Name}: {optDepends.Single().Description}",
                    Is.EqualTo(PackageFixtures.MinimalPackageOptDepend));
            });
        }

        [Test]
        public void ScalarMetadata_MatchesTheFixture()
        {
            Assert.Multiple(() =>
            {
                Assert.That(_pkg.GetBase(), Is.EqualTo(PackageFixtures.MinimalPackageBase));
                Assert.That(_pkg.GetUrl(), Is.EqualTo(PackageFixtures.MinimalPackageUrl));
                Assert.That(_pkg.GetPackager(), Is.EqualTo(PackageFixtures.MinimalPackagePackager));
                Assert.That(_pkg.GetInstalledSize(), Is.EqualTo(PackageFixtures.MinimalPackageInstalledSize));
                Assert.That(
                    _pkg.GetBuildDate(),
                    Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(PackageFixtures.MinimalPackageBuildDateUnixSeconds)));
            });
        }

        [Test]
        public void EveryMember_AfterDispose_ThrowsObjectDisposedException()
        {
            // Every new member reads through the native handle, so none of them may dereference it
            // once it has been freed.
            _pkg.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(() => _pkg.GetFileName(), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => _pkg.GetSha256Sum(), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => _pkg.GetMd5Sum(), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => _pkg.GetLicenses(), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => _pkg.GetGroups(), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => _pkg.GetProvides(), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => _pkg.GetReplaces(), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => _pkg.GetMakeDepends(), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => _pkg.GetCheckDepends(), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(() => _pkg.GetInstallDate(), Throws.TypeOf<ObjectDisposedException>());
            });
        }
    }
}

using System;
using System.IO;
using PacmanManager.TestUtils;

namespace LibAlpmSharp.Test;

/// <summary>
/// Covers <see cref="LibAlpm.LoadPackageFile"/> against the committed fixture package.
/// </summary>
/// <remarks>
/// The handle is initialised against a temporary root and database path rather than the host's,
/// because loading a package file reads nothing from a database and these tests should not depend
/// on what the machine running them happens to have installed.
/// </remarks>
[TestFixture]
public class LoadPackageFileTests
{
    private string _tempDirectory = null!;
    private LibAlpm _alpm = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"libalpmsharp-{Guid.NewGuid():N}");
        var root = Path.Combine(_tempDirectory, "root");
        var dbPath = Path.Combine(_tempDirectory, "db");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(dbPath);

        _alpm = LibAlpm.Initialize(root, dbPath);
    }

    [TearDown]
    public void TearDown()
    {
        _alpm.Dispose();

        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [Test]
    public void LoadPackageFile_ReportsTheMetadataInThePackage()
    {
        // Act
        using var pkg = _alpm.LoadPackageFile(PackageFixtures.MinimalPackagePath);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(pkg.Name, Is.EqualTo(PackageFixtures.MinimalPackageName));
            Assert.That(pkg.Version, Is.EqualTo(PackageFixtures.MinimalPackageVersion));
            Assert.That(pkg.GetArchitecture(), Is.EqualTo(PackageFixtures.MinimalPackageArchitecture));
            Assert.That(pkg.Description, Is.EqualTo(PackageFixtures.MinimalPackageDescription));
        });
    }

    [Test]
    public void LoadPackageFile_WithoutAFullLoad_StillReportsMetadata()
    {
        // Act
        using var pkg = _alpm.LoadPackageFile(PackageFixtures.MinimalPackagePath, full: false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(pkg.Name, Is.EqualTo(PackageFixtures.MinimalPackageName));
            Assert.That(pkg.Version, Is.EqualTo(PackageFixtures.MinimalPackageVersion));
            Assert.That(pkg.GetArchitecture(), Is.EqualTo(PackageFixtures.MinimalPackageArchitecture));
        });
    }

    [Test]
    public void LoadPackageFile_WithAMissingFile_ThrowsAlpmException()
    {
        // Arrange
        var missing = Path.Combine(_tempDirectory, "not-there-1.0.0-1-x86_64.pkg.tar.zst");

        // Act & Assert
        Assert.Throws<AlpmException>(() => _alpm.LoadPackageFile(missing));
    }

    [Test]
    public void LoadPackageFile_WithAMalformedFile_ThrowsAlpmException()
    {
        // Arrange
        var malformed = Path.Combine(_tempDirectory, "garbage-1.0.0-1-x86_64.pkg.tar.zst");
        File.WriteAllText(malformed, "this is not a package");

        // Act & Assert
        Assert.Throws<AlpmException>(() => _alpm.LoadPackageFile(malformed));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void LoadPackageFile_WithNoPath_ThrowsArgumentException(string? path)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _alpm.LoadPackageFile(path!));
    }

    [Test]
    public void LoadPackageFile_AfterTheHandleIsDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        _alpm.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _alpm.LoadPackageFile(PackageFixtures.MinimalPackagePath));
    }

    [Test]
    public void Dispose_ReleasesTheNativeHandle()
    {
        // Arrange
        var pkg = _alpm.LoadPackageFile(PackageFixtures.MinimalPackagePath);

        // Act
        pkg.Dispose();

        // Assert - the handle is gone, so anything that would read through it must refuse rather
        // than dereference freed memory. Values read at load time stay available.
        Assert.Multiple(() =>
        {
            Assert.That(() => pkg.GetArchitecture(), Throws.TypeOf<ObjectDisposedException>());
            Assert.That(pkg.Name, Is.EqualTo(PackageFixtures.MinimalPackageName));
        });
    }

    [Test]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var pkg = _alpm.LoadPackageFile(PackageFixtures.MinimalPackagePath);

        // Act & Assert - a second free of the same handle would be a double free.
        pkg.Dispose();
        Assert.DoesNotThrow(() => pkg.Dispose());
    }
}

using System;
using System.IO;
using System.Linq;
using LibAlpmSharp.Interop;
using NUnit.Framework;
using PacmanManager.TestUtils;

namespace LibAlpmSharp.Test;

/// <summary>
/// Covers <see cref="AlpmPackage"/> against a package read back out of a local database, which is
/// the shape a package has once it is installed: it belongs to a database, it carries an install
/// date, and it knows what else on the system depends on it.
/// </summary>
/// <remarks>
/// The database is seeded under a temporary root by <see cref="LocalPackageDatabase"/> rather than
/// being the host's own at <c>/var/lib/pacman</c>. These tests used to read the host's database and
/// assert against whichever build of <c>pacman</c> it carried, which meant they only ran on Arch and
/// only ever asserted that a value was non-empty. Seeding lets each of them pin an exact value, and
/// lets all of them run anywhere libalpm does.
/// </remarks>
[TestFixture]
public class AlpmPackageTests
{
    private LocalPackageDatabase _seeded = null!;
    private LibAlpm _alpm = null!;
    private IPackage _pkg = null!;

    [SetUp]
    public void SetUp()
    {
        _seeded = LocalPackageDatabase.Seed();
        _alpm = LibAlpm.Initialize(_seeded.Root, _seeded.DbPath);
        _pkg = _alpm.GetLocalDatabase().GetPackage(PackageFixtures.MinimalPackageName)
               ?? throw new InvalidOperationException(
                   $"The seeded local database has no package named '{PackageFixtures.MinimalPackageName}'.");
    }

    [TearDown]
    public void TearDown()
    {
        // A package that came out of a database is owned by the handle rather than by the caller,
        // so releasing the handle is what releases it.
        _alpm.Dispose();
        _seeded.Dispose();
    }

    [Test]
    public void Name_ReturnsPackageName()
    {
        Assert.That(_pkg.Name, Is.EqualTo(PackageFixtures.MinimalPackageName));
    }

    [Test]
    public void Version_ReturnsPackageVersion()
    {
        Assert.That(_pkg.Version, Is.EqualTo(PackageFixtures.MinimalPackageVersion));
    }

    [Test]
    public void Description_ReturnsPackageDescription()
    {
        Assert.That(_pkg.Description, Is.EqualTo(PackageFixtures.MinimalPackageDescription));
    }

    [Test]
    public void ToString_ReturnsNameAndVersion()
    {
        Assert.That(
            _pkg.ToString(),
            Is.EqualTo($"{PackageFixtures.MinimalPackageName} {PackageFixtures.MinimalPackageVersion}"));
    }

    [Test]
    public void GetBase_ReturnsPackageBase()
    {
        Assert.That(_pkg.GetBase(), Is.EqualTo(PackageFixtures.MinimalPackageBase));
    }

    [Test]
    public void GetUrl_ReturnsPackageUrl()
    {
        Assert.That(_pkg.GetUrl(), Is.EqualTo(PackageFixtures.MinimalPackageUrl));
    }

    [Test]
    public void GetArchitecture_ReturnsPackageArchitecture()
    {
        Assert.That(_pkg.GetArchitecture(), Is.EqualTo(PackageFixtures.MinimalPackageArchitecture));
    }

    [Test]
    public void GetPackager_ReturnsPackagerName()
    {
        Assert.That(_pkg.GetPackager(), Is.EqualTo(PackageFixtures.MinimalPackagePackager));
    }

    [Test]
    public void GetInstalledSize_ReturnsTheRecordedSize()
    {
        Assert.That(_pkg.GetInstalledSize(), Is.EqualTo(PackageFixtures.MinimalPackageInstalledSize));
    }

    [Test]
    public void GetDownloadSize_ForAnInstalledPackage_IsZero()
    {
        // A download size is what a sync database reports for a package still to be fetched. An
        // installed one has nothing left to download, and libalpm reports zero rather than the
        // installed size, which is the trap this pins.
        Assert.That(_pkg.GetDownloadSize(), Is.Zero);
    }

    [Test]
    public void GetBuildDate_ReturnsTheRecordedBuildDate()
    {
        Assert.That(
            _pkg.GetBuildDate(),
            Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(PackageFixtures.MinimalPackageBuildDateUnixSeconds)));
    }

    [Test]
    public void GetInstallDate_ForAnInstalledPackage_ReturnsTheRecordedInstallDate()
    {
        Assert.That(
            _pkg.GetInstallDate(),
            Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(LocalPackageDatabase.InstallDateUnixSeconds)));
    }

    [Test]
    public void GetInstallDate_IsNotBeforeGetBuildDate()
    {
        Assert.That(_pkg.GetInstallDate(), Is.GreaterThanOrEqualTo(_pkg.GetBuildDate()));
    }

    [Test]
    public void MultiplePropertyAccess_ReturnsConsistentValues()
    {
        // Each read marshals a fresh string out of the native handle, so reading twice is the check
        // that nothing is consumed on the way out.
        var (name, version, description) = (_pkg.Name, _pkg.Version, _pkg.Description);

        Assert.Multiple(() =>
        {
            Assert.That(_pkg.Name, Is.EqualTo(name));
            Assert.That(_pkg.Version, Is.EqualTo(version));
            Assert.That(_pkg.Description, Is.EqualTo(description));
        });
    }

    [Test]
    public void AllMethods_DoNotThrowExceptions()
    {
        // A smoke test over the whole member surface: the members with exact expectations are
        // covered above, and this catches a new one that faults on an installed package.
        Assert.Multiple(() =>
        {
            Assert.That(() => _pkg.Name, Throws.Nothing);
            Assert.That(() => _pkg.Version, Throws.Nothing);
            Assert.That(() => _pkg.Description, Throws.Nothing);
            Assert.That(() => _pkg.ToString(), Throws.Nothing);
            Assert.That(() => _pkg.GetBase(), Throws.Nothing);
            Assert.That(() => _pkg.GetUrl(), Throws.Nothing);
            Assert.That(() => _pkg.GetArchitecture(), Throws.Nothing);
            Assert.That(() => _pkg.GetPackager(), Throws.Nothing);
            Assert.That(() => _pkg.GetInstalledSize(), Throws.Nothing);
            Assert.That(() => _pkg.GetDownloadSize(), Throws.Nothing);
            Assert.That(() => _pkg.GetBuildDate(), Throws.Nothing);
            Assert.That(() => _pkg.GetInstallDate(), Throws.Nothing);
            Assert.That(() => _pkg.GetFileName(), Throws.Nothing);
            Assert.That(() => _pkg.GetSha256Sum(), Throws.Nothing);
            Assert.That(() => _pkg.GetMd5Sum(), Throws.Nothing);
            Assert.That(() => _pkg.GetLicenses(), Throws.Nothing);
            Assert.That(() => _pkg.GetGroups(), Throws.Nothing);
        });
    }

    [Test]
    public void GetDependencies_ReturnsTheDeclaredDependencies()
    {
        Assert.That(
            _pkg.GetDependencies().Select(d => d.ToString()),
            Is.EqualTo(new[] { PackageFixtures.MinimalPackageDepend }));
    }

    [Test]
    public void GetDependencies_DependencyHasValidProperties()
    {
        var dependency = _pkg.GetDependencies().Single();

        // The fixture depends on a bare package name, so there is no version constraint to carry
        // and no reason: those belong to a versioned dependency and to an optional one.
        Assert.Multiple(() =>
        {
            Assert.That(dependency.Name, Is.EqualTo(PackageFixtures.MinimalPackageDepend));
            Assert.That(dependency.Version, Is.Null);
            Assert.That(dependency.Description, Is.Null);
            Assert.That(dependency.Modifier, Is.EqualTo(AlpmDepMod.ALPM_DEP_MOD_ANY));
            Assert.That(dependency.ToString(), Is.EqualTo(PackageFixtures.MinimalPackageDepend));
        });
    }

    [Test]
    public void GetOptionalDependencies_ReturnsTheDeclaredOptionalDependencies()
    {
        var optionalDependency = _pkg.GetOptionalDependencies().Single();

        // pacman spells an optional dependency "name: reason", and AlpmDependency keeps the reason
        // in Description rather than in ToString().
        Assert.Multiple(() =>
        {
            Assert.That(optionalDependency.Name, Is.EqualTo("git"));
            Assert.That(optionalDependency.Description, Is.EqualTo("for the sync command"));
            Assert.That(
                $"{optionalDependency.Name}: {optionalDependency.Description}",
                Is.EqualTo(PackageFixtures.MinimalPackageOptDepend));
        });
    }

    [Test]
    public void GetRequiredBy_ReturnsThePackagesThatDependOnIt()
    {
        // libalpm computes this by walking the local database, so it only reports anything because
        // the seeded database holds a second package that depends on this one.
        Assert.That(
            _pkg.GetRequiredBy(),
            Is.EqualTo(new[] { LocalPackageDatabase.DependentPackageName }));
    }

    [Test]
    public void GetOptionalFor_ReturnsThePackagesThatOptionallyDependOnIt()
    {
        Assert.That(
            _pkg.GetOptionalFor(),
            Is.EqualTo(new[] { LocalPackageDatabase.DependentPackageName }));
    }

    [Test]
    public void GetConflicts_ReturnsTheDeclaredConflicts()
    {
        Assert.That(
            _pkg.GetConflicts().Select(c => c.ToString()),
            Is.EqualTo(new[] { PackageFixtures.MinimalPackageConflict }));
    }

    [Test]
    public void GetDependencies_CanBeCalledMultipleTimes()
    {
        // Each call builds a fresh managed list off the same native list, which must not consume or
        // free it.
        Assert.That(
            _pkg.GetDependencies().Select(d => d.ToString()),
            Is.EqualTo(_pkg.GetDependencies().Select(d => d.ToString())));
    }

    [Test]
    public void AllNewMethods_DoNotThrowExceptions()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => _pkg.GetDependencies(), Throws.Nothing);
            Assert.That(() => _pkg.GetOptionalDependencies(), Throws.Nothing);
            Assert.That(() => _pkg.GetRequiredBy(), Throws.Nothing);
            Assert.That(() => _pkg.GetOptionalFor(), Throws.Nothing);
            Assert.That(() => _pkg.GetConflicts(), Throws.Nothing);
            Assert.That(() => _pkg.GetProvides(), Throws.Nothing);
            Assert.That(() => _pkg.GetReplaces(), Throws.Nothing);
            Assert.That(() => _pkg.GetMakeDepends(), Throws.Nothing);
            Assert.That(() => _pkg.GetCheckDepends(), Throws.Nothing);
        });
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
        // The local database does record build time dependencies, so an installed package reports
        // them just as a file loaded one does.
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

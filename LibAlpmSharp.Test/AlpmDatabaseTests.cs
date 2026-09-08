using System;
using System.Linq;
using NUnit.Framework;
using PacmanManager.TestUtils;

namespace LibAlpmSharp.Test;

/// <summary>
/// Covers <see cref="AlpmDatabase"/> against a local database seeded under a temporary root by
/// <see cref="LocalPackageDatabase"/>.
/// </summary>
/// <remarks>
/// These tests used to initialise libalpm at <c>/</c> and <c>/var/lib/pacman</c>. That needs a
/// writable system root, so off Arch they reported a warning and stopped rather than running, and
/// the ones that did run could only assert that the host happened to have packages installed. A
/// temporary root removes both problems: libalpm can create its database, and the contents are
/// known.
/// </remarks>
[TestFixture]
public class AlpmDatabaseTests
{
    private LocalPackageDatabase _seeded = null!;
    private LibAlpm _alpm = null!;

    [SetUp]
    public void SetUp()
    {
        _seeded = LocalPackageDatabase.Seed();
        _alpm = LibAlpm.Initialize(_seeded.Root, _seeded.DbPath);
    }

    [TearDown]
    public void TearDown()
    {
        _alpm.Dispose();
        _seeded.Dispose();
    }

    [Test]
    public void GetLocalDatabase_ReturnsValidDatabase()
    {
        var localDb = _alpm.GetLocalDatabase();

        Assert.Multiple(() =>
        {
            Assert.That(localDb.IsLocal, Is.True);
            // libalpm names the local database "local" whatever root it was pointed at.
            Assert.That(localDb.Name, Is.EqualTo("local"));
        });
    }

    [Test]
    public void GetSyncDatabases_ReturnsListOfDatabases()
    {
        // Registering a sync database is a bookkeeping operation: no file has to exist for it, and
        // nothing is fetched, so the names here are stand ins rather than databases being read.
        _alpm.RegisterSyncDatabase("core");
        _alpm.RegisterSyncDatabase("extra");
        _alpm.RegisterSyncDatabase("multilib");

        var syncDbs = _alpm.GetSyncDatabases().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(syncDbs.Select(d => d.Name), Is.EqualTo(new[] { "core", "extra", "multilib" }));
            Assert.That(syncDbs.Select(d => d.IsLocal), Has.All.False);
        });
    }

    [Test]
    public void RegisterSyncDatabase_CreatesNewDatabase()
    {
        var db = _alpm.RegisterSyncDatabase("core");

        Assert.Multiple(() =>
        {
            Assert.That(db.Name, Is.EqualTo("core"));
            Assert.That(db.IsLocal, Is.False);
            Assert.That(_alpm.GetSyncDatabases().Select(d => d.Name), Does.Contain("core"));
        });
    }

    [Test]
    public void RegisterSyncDatabase_WithNullName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _alpm.RegisterSyncDatabase(null!));
    }

    [Test]
    public void RegisterSyncDatabase_WithEmptyName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _alpm.RegisterSyncDatabase(""));
    }

    [Test]
    public void AlpmDatabase_GetServers_ReturnsListOfServers()
    {
        var coreDb = _alpm.RegisterSyncDatabase("core");
        const string testServer = "https://mirror.example.com/$repo/os/$arch";
        coreDb.AddServer(testServer);

        Assert.That(coreDb.GetServers(), Is.EqualTo(new[] { testServer }));
    }

    [Test]
    public void AlpmDatabase_AddServer_AddsServerToList()
    {
        var db = _alpm.RegisterSyncDatabase("extra");
        const string serverUrl = "https://example.com/repo/$repo/os/$arch";

        db.AddServer(serverUrl);

        Assert.That(db.GetServers(), Does.Contain(serverUrl));
    }

    [Test]
    public void AlpmDatabase_RemoveServer_RemovesServerFromList()
    {
        var db = _alpm.RegisterSyncDatabase("multilib");
        const string serverUrl = "https://example.com/repo/$repo/os/$arch";
        db.AddServer(serverUrl);

        var removed = db.RemoveServer(serverUrl);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(db.GetServers(), Does.Not.Contain(serverUrl));
        });
    }

    [Test]
    public void AlpmDatabase_IsValid_ForTheSeededLocalDatabase_ReturnsTrue()
    {
        // Validity is the on-disk format check: a local database is valid when its ALPM_DB_VERSION
        // is one libalpm knows, which is what the seeding writes.
        Assert.That(_alpm.GetLocalDatabase().IsValid(), Is.True);
    }

    [Test]
    public void GetPackage_WithValidName_ReturnsPackage()
    {
        var pkg = _alpm.GetLocalDatabase().GetPackage(PackageFixtures.MinimalPackageName);

        Assert.That(pkg, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(pkg!.Name, Is.EqualTo(PackageFixtures.MinimalPackageName));
            Assert.That(pkg.Version, Is.EqualTo(PackageFixtures.MinimalPackageVersion));
        });
    }

    [Test]
    public void GetPackage_WithInvalidName_ReturnsNull()
    {
        Assert.That(_alpm.GetLocalDatabase().GetPackage("this-package-does-not-exist-xyz123"), Is.Null);
    }

    [Test]
    public void GetPackage_WithNullName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _alpm.GetLocalDatabase().GetPackage(null!));
    }

    [Test]
    public void GetPackages_ReturnsListOfPackages()
    {
        var packages = _alpm.GetLocalDatabase().GetPackages();

        Assert.That(packages.Select(p => p.Name), Is.EquivalentTo(LocalPackageDatabase.PackageNames));
    }

    [Test]
    public void Search_WithValidTerm_ReturnsMatchingPackages()
    {
        // Both seeded packages share the prefix, so a search for it is a search that matches more
        // than one thing; the term below matches only the second.
        Assert.Multiple(() =>
        {
            Assert.That(
                _alpm.GetLocalDatabase().Search("pacmanmanager-test").Select(p => p.Name),
                Is.EquivalentTo(LocalPackageDatabase.PackageNames));
            Assert.That(
                _alpm.GetLocalDatabase().Search("dependent").Select(p => p.Name),
                Is.EqualTo(new[] { LocalPackageDatabase.DependentPackageName }));
        });
    }

    [Test]
    public void Search_WithNoMatch_ReturnsNothing()
    {
        Assert.That(_alpm.GetLocalDatabase().Search("this-package-does-not-exist-xyz123"), Is.Empty);
    }

    [Test]
    public void Search_WithNullTerms_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _alpm.GetLocalDatabase().Search(null!));
    }

    [Test]
    public void Search_WithEmptyTerms_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _alpm.GetLocalDatabase().Search());
    }
}

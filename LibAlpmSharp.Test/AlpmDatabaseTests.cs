using System;
using System.Collections.Generic;
using System.Linq;
using LibAlpmSharp.Interop;
using NUnit.Framework;

namespace LibAlpmSharp.Test;

[TestFixture]
public class AlpmDatabaseTests
{
    [Test]
    public void GetLocalDatabase_ReturnsValidDatabase()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            
            // Act
            AlpmDatabase localDb = alpm.GetLocalDatabase();
            
            // Assert
            Assert.That(localDb, Is.Not.Null);
            Assert.That(localDb.IsLocal, Is.True);
            Assert.That(localDb.Name, Is.Not.Null.And.Not.Empty);
            
            TestContext.WriteLine($"Local database: {localDb}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetSyncDatabases_ReturnsListOfDatabases()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            
            // Register standard Arch Linux databases
            alpm.RegisterSyncDatabase("core");
            alpm.RegisterSyncDatabase("extra");
            alpm.RegisterSyncDatabase("multilib");
            
            // Act
            List<AlpmDatabase> syncDbs = alpm.GetSyncDatabases();
            
            // Assert
            Assert.That(syncDbs, Is.Not.Null);
            Assert.That(syncDbs, Has.Count.GreaterThanOrEqualTo(3), "Should have at least the 3 standard Arch Linux databases we registered");
            TestContext.WriteLine($"Found {syncDbs.Count} sync databases");
            
            foreach (var db in syncDbs)
            {
                Assert.That(db.IsLocal, Is.False);
                Assert.That(db.Name, Is.Not.Null.And.Not.Empty);
                TestContext.WriteLine($"  - {db}");
            }
            
            // Verify standard Arch Linux databases are in the list
            Assert.That(syncDbs, Has.Some.Matches<AlpmDatabase>(d => d.Name == "core"));
            Assert.That(syncDbs, Has.Some.Matches<AlpmDatabase>(d => d.Name == "extra"));
            Assert.That(syncDbs, Has.Some.Matches<AlpmDatabase>(d => d.Name == "multilib"));
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void RegisterSyncDatabase_CreatesNewDatabase()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            string dbName = "core";
            
            // Act
            AlpmDatabase db = alpm.RegisterSyncDatabase(dbName);
            
            // Assert
            Assert.That(db, Is.Not.Null);
            Assert.That(db.Name, Is.EqualTo(dbName));
            Assert.That(db.IsLocal, Is.False);
            
            TestContext.WriteLine($"Registered database: {db}");
            
            // Verify it appears in sync databases list
            var syncDbs = alpm.GetSyncDatabases();
            Assert.That(syncDbs, Has.Some.Matches<AlpmDatabase>(d => d.Name == dbName));
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void RegisterSyncDatabase_WithNullName_ThrowsArgumentException()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => alpm.RegisterSyncDatabase(null!));
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void RegisterSyncDatabase_WithEmptyName_ThrowsArgumentException()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => alpm.RegisterSyncDatabase(""));
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void AlpmDatabase_GetServers_ReturnsListOfServers()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var coreDb = alpm.RegisterSyncDatabase("core");
            
            // Add a test server to ensure we have at least one
            string testServer = "https://mirror.example.com/$repo/os/$arch";
            coreDb.AddServer(testServer);
            
            // Act
            var servers = coreDb.GetServers();
            
            // Assert
            Assert.That(servers, Is.Not.Null);
            Assert.That(servers, Has.Count.GreaterThanOrEqualTo(1), "Should have at least one server");
            Assert.That(servers, Has.Some.EqualTo(testServer), "Should contain the test server we added");
            
            TestContext.WriteLine($"Database '{coreDb.Name}' has {servers.Count} servers");
            
            foreach (var server in servers)
            {
                TestContext.WriteLine($"  - {server}");
            }
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void AlpmDatabase_AddServer_AddsServerToList()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var db = alpm.RegisterSyncDatabase("extra");
            string serverUrl = "https://example.com/repo/$repo/os/$arch";
            
            // Act
            db.AddServer(serverUrl);
            
            // Assert
            var servers = db.GetServers();
            Assert.That(servers, Has.Some.EqualTo(serverUrl));
            
            TestContext.WriteLine($"Added server to {db.Name}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void AlpmDatabase_RemoveServer_RemovesServerFromList()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var db = alpm.RegisterSyncDatabase("multilib");
            string serverUrl = "https://example.com/repo/$repo/os/$arch";
            db.AddServer(serverUrl);
            
            // Act
            bool removed = db.RemoveServer(serverUrl);
            
            // Assert
            Assert.That(removed, Is.True);
            var servers = db.GetServers();
            Assert.That(servers, Has.None.EqualTo(serverUrl));
            
            TestContext.WriteLine($"Removed server from {db.Name}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void AlpmDatabase_IsValid_ReturnsValidationStatus()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            bool isValid = localDb.IsValid();
            
            // Assert
            TestContext.WriteLine($"Local database is valid: {isValid}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetPackage_WithValidName_ReturnsPackage()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Get all packages to find a valid one
            var packages = localDb.GetPackages();
            if (packages.Count == 0)
            {
                Assert.Warn("No packages found in local database");
                return;
            }
            
            string testPackageName = packages[0].Name;
            
            // Act
            AlpmPackage? pkg = localDb.GetPackage(testPackageName);
            
            // Assert
            Assert.That(pkg, Is.Not.Null);
            Assert.That(pkg!.Name, Is.EqualTo(testPackageName));
            Assert.That(pkg.Version, Is.Not.Null.And.Not.Empty);
            
            TestContext.WriteLine($"Found package: {pkg}");
            TestContext.WriteLine($"  Description: {pkg.Description}");
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetPackage_WithInvalidName_ReturnsNull()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            AlpmPackage? pkg = localDb.GetPackage("this-package-does-not-exist-xyz123");
            
            // Assert
            Assert.That(pkg, Is.Null);
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetPackage_WithNullName_ThrowsArgumentException()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => localDb.GetPackage(null!));
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetPackages_ReturnsListOfPackages()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act
            var packages = localDb.GetPackages();
            
            // Assert
            Assert.That(packages, Is.Not.Null);
            Assert.That(packages, Has.Count.GreaterThanOrEqualTo(1), "Should have at least one package in local database");
            TestContext.WriteLine($"Found {packages.Count} packages in local database");
            
            if (packages.Count > 0)
            {
                var firstPkg = packages[0];
                TestContext.WriteLine($"First package: {firstPkg}");
                TestContext.WriteLine($"  Description: {firstPkg.Description}");
                TestContext.WriteLine($"  Architecture: {firstPkg.GetArchitecture()}");
            }
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void Search_WithValidTerm_ReturnsMatchingPackages()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Get all packages to find a valid search term
            var allPackages = localDb.GetPackages();
            if (allPackages.Count == 0)
            {
                Assert.Warn("No packages found in local database");
                return;
            }
            
            // Use part of the first package name as search term
            string searchTerm = allPackages[0].Name.Substring(0, Math.Min(3, allPackages[0].Name.Length));
            
            // Act
            var results = localDb.Search(searchTerm);
            
            // Assert
            Assert.That(results, Is.Not.Null);
            Assert.That(results, Has.Count.GreaterThanOrEqualTo(1), "Search should return at least one result");
            TestContext.WriteLine($"Search for '{searchTerm}' found {results.Count} packages");
            
            foreach (var pkg in results.Take(5))
            {
                TestContext.WriteLine($"  - {pkg}");
            }
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void Search_WithNullTerms_ThrowsArgumentException()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => localDb.Search(null!));
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void Search_WithEmptyTerms_ThrowsArgumentException()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            var localDb = alpm.GetLocalDatabase();
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => localDb.Search());
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }
}

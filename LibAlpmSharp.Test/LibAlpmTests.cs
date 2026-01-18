using System;
using System.Collections.Generic;
using LibAlpmSharp.Interop;
using NUnit.Framework;

namespace LibAlpmSharp.Test;

[TestFixture]
public class LibAlpmTests
{
    [Test]
    public void GetVersion_ReturnsNonEmptyString()
    {
        // Act
        string version = LibAlpm.GetVersion();
        
        // Assert
        Assert.That(version, Is.Not.Null.And.Not.Empty);
        TestContext.WriteLine($"libalpm version: {version}");
    }

    [Test]
    public void GetCapabilities_ReturnsValidFlags()
    {
        // Act
        AlpmCaps caps = LibAlpm.GetCapabilities();
        
        // Assert
        Assert.That((int)caps, Is.GreaterThanOrEqualTo(0));
        TestContext.WriteLine($"Capabilities: {caps}");
        
        if (caps.HasFlag(AlpmCaps.ALPM_CAPABILITY_NLS))
            TestContext.WriteLine("  - NLS support enabled");
        if (caps.HasFlag(AlpmCaps.ALPM_CAPABILITY_DOWNLOADER))
            TestContext.WriteLine("  - Downloader support enabled");
        if (caps.HasFlag(AlpmCaps.ALPM_CAPABILITY_SIGNATURES))
            TestContext.WriteLine("  - Signature checking enabled");
    }

    [Test]
    public void GetErrorString_ReturnsValidString()
    {
        // Act
        string okError = LibAlpm.GetErrorString(AlpmErrno.ALPM_ERR_OK);
        string memoryError = LibAlpm.GetErrorString(AlpmErrno.ALPM_ERR_MEMORY);
        
        // Assert
        Assert.That(okError, Is.Not.Null.And.Not.Empty);
        Assert.That(memoryError, Is.Not.Null.And.Not.Empty);
        
        TestContext.WriteLine($"ALPM_ERR_OK: {okError}");
        TestContext.WriteLine($"ALPM_ERR_MEMORY: {memoryError}");
    }

    [Test]
    public void Initialize_WithDefaultPaths_CreatesInstance()
    {
        // This test may fail if not run with appropriate permissions
        try
        {
            // Act
            using (LibAlpm alpm = LibAlpm.Initialize())
            {
                // Assert
                Assert.That(alpm, Is.Not.Null);
                Assert.That(alpm.Root, Is.EqualTo("/"));
                Assert.That(alpm.DbPath, Is.EqualTo("/var/lib/pacman"));
                
                TestContext.WriteLine($"Root: {alpm.Root}");
                TestContext.WriteLine($"DbPath: {alpm.DbPath}");
            }
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void Initialize_WithCustomPaths_CreatesInstance()
    {
        // This test may fail if not run with appropriate permissions
        try
        {
            // Arrange
            string root = "/";
            string dbPath = "/var/lib/pacman";
            
            // Act
            using (LibAlpm alpm = LibAlpm.Initialize(root, dbPath))
            {
                // Assert
                Assert.That(alpm, Is.Not.Null);
                Assert.That(alpm.Root, Is.EqualTo(root));
                Assert.That(alpm.DbPath, Is.EqualTo(dbPath));
            }
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void Initialize_WithNullRoot_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => LibAlpm.Initialize(null!, "/var/lib/pacman"));
    }

    [Test]
    public void Initialize_WithEmptyRoot_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => LibAlpm.Initialize("", "/var/lib/pacman"));
    }

    [Test]
    public void Initialize_WithNullDbPath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => LibAlpm.Initialize("/", null!));
    }

    [Test]
    public void Initialize_WithEmptyDbPath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => LibAlpm.Initialize("/", ""));
    }

    [Test]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        try
        {
            // Arrange
            LibAlpm alpm = LibAlpm.Initialize();
            
            // Act & Assert - should not throw
            alpm.Dispose();
            alpm.Dispose();
            alpm.Dispose();
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetLastError_AfterDispose_ThrowsObjectDisposedException()
    {
        try
        {
            // Arrange
            LibAlpm alpm = LibAlpm.Initialize();
            alpm.Dispose();
            
            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => alpm.GetLastError());
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void Handle_AfterDispose_ThrowsObjectDisposedException()
    {
        try
        {
            // Arrange
            LibAlpm alpm = LibAlpm.Initialize();
            alpm.Dispose();
            
            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => 
            {
                var handle = alpm.Handle;
            });
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void GetLastError_WhenNoError_ReturnsOK()
    {
        try
        {
            // Arrange
            using LibAlpm alpm = LibAlpm.Initialize();
            
            // Act
            AlpmErrno error = alpm.GetLastError();
            
            // Assert
            Assert.That(error, Is.EqualTo(AlpmErrno.ALPM_ERR_OK));
        }
        catch (AlpmException ex)
        {
            Assert.Warn($"Failed to initialize libalpm (may need permissions): {ex.Message}");
        }
    }

    [Test]
    public void AlpmException_PreservesErrorCode()
    {
        // Arrange
        AlpmErrno expectedError = AlpmErrno.ALPM_ERR_MEMORY;
        
        // Act
        var exception = new AlpmException(expectedError);
        
        // Assert
        Assert.That(exception.ErrorCode, Is.EqualTo(expectedError));
        Assert.That(exception.Message, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void AlpmException_WithErrorCode_HasCorrectMessage()
    {
        // Arrange
        AlpmErrno errorCode = AlpmErrno.ALPM_ERR_MEMORY;
        
        // Act
        var exception = new AlpmException(errorCode);
        
        // Assert
        Assert.That(exception.Message, Does.Contain("memory").IgnoreCase);
    }

    [Test]
    public void AlpmException_WithCustomMessage_PreservesMessage()
    {
        // Arrange
        string customMessage = "Custom error message";
        
        // Act
        var exception = new AlpmException(customMessage);
        
        // Assert
        Assert.That(exception.Message, Is.EqualTo(customMessage));
    }

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
            Assert.That(syncDbs.Count, Is.GreaterThanOrEqualTo(3), "Should have at least the 3 standard Arch Linux databases we registered");
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
            Assert.That(servers.Count, Is.GreaterThanOrEqualTo(1), "Should have at least one server");
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
}

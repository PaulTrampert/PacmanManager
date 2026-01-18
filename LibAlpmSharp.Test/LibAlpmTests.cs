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

    
}

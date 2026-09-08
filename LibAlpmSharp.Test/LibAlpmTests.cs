using System;
using System.Collections.Generic;
using LibAlpmSharp.Interop;
using NUnit.Framework;
using PacmanManager.TestUtils;

namespace LibAlpmSharp.Test;

[TestFixture]
public class LibAlpmTests
{
    // A writable root and database path, so that a test needing nothing more than an initialised
    // handle gets one on any host rather than only where /var/lib/pacman happens to be writable.
    private LocalPackageDatabase _seeded = null!;

    [SetUp]
    public void SetUp()
    {
        _seeded = LocalPackageDatabase.Seed();
    }

    [TearDown]
    public void TearDown()
    {
        _seeded.Dispose();
    }

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
        // The default paths are the whole point of this one, so unlike every other test here it
        // cannot be pointed at a temporary root: it only runs where "/var/lib/pacman" is a pacman
        // database the current user may open, and warns rather than failing anywhere else.
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
        // Act
        using LibAlpm alpm = LibAlpm.Initialize(_seeded.Root, _seeded.DbPath);

        // Assert - the paths are reported back exactly as handed in, rather than as libalpm
        // canonicalises them internally.
        Assert.Multiple(() =>
        {
            Assert.That(alpm.Root, Is.EqualTo(_seeded.Root));
            Assert.That(alpm.DbPath, Is.EqualTo(_seeded.DbPath));
        });
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
        // Arrange
        LibAlpm alpm = LibAlpm.Initialize(_seeded.Root, _seeded.DbPath);

        // Act & Assert - only the first release reaches libalpm; the rest are no-ops rather than a
        // double free.
        alpm.Dispose();
        Assert.That(() =>
        {
            alpm.Dispose();
            alpm.Dispose();
        }, Throws.Nothing);
    }

    [Test]
    public void GetLastError_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        LibAlpm alpm = LibAlpm.Initialize(_seeded.Root, _seeded.DbPath);
        alpm.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => alpm.GetLastError());
    }

    [Test]
    public void Handle_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        LibAlpm alpm = LibAlpm.Initialize(_seeded.Root, _seeded.DbPath);
        alpm.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() =>
        {
            var handle = alpm.Handle;
        });
    }

    [Test]
    public void GetLastError_WhenNoError_ReturnsOK()
    {
        // Arrange
        using LibAlpm alpm = LibAlpm.Initialize(_seeded.Root, _seeded.DbPath);

        // Act
        AlpmErrno error = alpm.GetLastError();

        // Assert
        Assert.That(error, Is.EqualTo(AlpmErrno.ALPM_ERR_OK));
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

using System.Runtime.InteropServices;
using LibAlpmSharp.Interop;
using NUnit.Framework;
using PacmanManager.TestUtils;

namespace LibAlpmSharp.Test;

[TestFixture]
public class InteropTests
{
    [Test]
    public unsafe void TestLibraryVersion()
    {
        // Test that we can call the library and get the version
        byte* versionPtr = NativeMethods.alpm_version();
        Assert.That((IntPtr)versionPtr, Is.Not.EqualTo(IntPtr.Zero), "alpm_version() should not return null");
        
        string? version = Marshal.PtrToStringUTF8((IntPtr)versionPtr);
        Assert.That(version, Is.Not.Null.And.Not.Empty, "Version string should not be null or empty");
        
        TestContext.WriteLine($"libalpm version: {version}");
    }

    [Test]
    public void TestLibraryCapabilities()
    {
        // Test that we can get capabilities
        int caps = NativeMethods.alpm_capabilities();
        Assert.That(caps, Is.GreaterThanOrEqualTo(0), "Capabilities should be >= 0");
        
        TestContext.WriteLine($"libalpm capabilities: {caps}");
        
        if ((caps & (int)AlpmCaps.ALPM_CAPABILITY_NLS) != 0)
            TestContext.WriteLine("  - NLS support enabled");
        if ((caps & (int)AlpmCaps.ALPM_CAPABILITY_DOWNLOADER) != 0)
            TestContext.WriteLine("  - Downloader support enabled");
        if ((caps & (int)AlpmCaps.ALPM_CAPABILITY_SIGNATURES) != 0)
            TestContext.WriteLine("  - Signature checking enabled");
    }

    [Test]
    public unsafe void TestInitializeAndRelease()
    {
        AlpmErrno err;

        // A temporary root rather than "/", so that this proves the initialize/release round trip
        // on any host instead of only where /var/lib/pacman is writable.
        using var seeded = LocalPackageDatabase.Seed();
        IntPtr root = Marshal.StringToHGlobalAnsi(seeded.Root);
        IntPtr dbpath = Marshal.StringToHGlobalAnsi(seeded.DbPath);

        try
        {
            IntPtr handle = NativeMethods.alpm_initialize(
                (byte*)root.ToPointer(),
                (byte*)dbpath.ToPointer(),
                &err);

            Assert.That(handle, Is.Not.EqualTo(IntPtr.Zero),
                $"alpm_initialize failed: {Marshal.PtrToStringUTF8((IntPtr)NativeMethods.alpm_strerror(err))}");

            // libalpm hands the paths back canonicalised, which means with a trailing separator.
            string? rootStr = Marshal.PtrToStringUTF8((IntPtr)NativeMethods.alpm_option_get_root(handle));
            string? dbpathStr = Marshal.PtrToStringUTF8((IntPtr)NativeMethods.alpm_option_get_dbpath(handle));

            Assert.Multiple(() =>
            {
                Assert.That(rootStr, Is.EqualTo($"{seeded.Root}/"));
                Assert.That(dbpathStr, Is.EqualTo($"{seeded.DbPath}/"));
            });

            int result = NativeMethods.alpm_release(handle);
            Assert.That(result, Is.EqualTo(0), "Release should return 0 on success");
        }
        finally
        {
            Marshal.FreeHGlobal(root);
            Marshal.FreeHGlobal(dbpath);
        }
    }

    [Test]
    public unsafe void TestErrorHandling()
    {
        // Test error string retrieval for various error codes
        var testErrors = new[]
        {
            AlpmErrno.ALPM_ERR_OK,
            AlpmErrno.ALPM_ERR_MEMORY,
            AlpmErrno.ALPM_ERR_SYSTEM,
            AlpmErrno.ALPM_ERR_BADPERMS,
            AlpmErrno.ALPM_ERR_PKG_NOT_FOUND
        };

        foreach (var errorCode in testErrors)
        {
            byte* errStrPtr = NativeMethods.alpm_strerror(errorCode);
            Assert.That((IntPtr)errStrPtr, Is.Not.EqualTo(IntPtr.Zero), 
                $"Error string for {errorCode} should not be null");
            
            string? errStr = Marshal.PtrToStringUTF8((IntPtr)errStrPtr);
            Assert.That(errStr, Is.Not.Null.And.Not.Empty,
                $"Error string for {errorCode} should not be null or empty");
            
            TestContext.WriteLine($"{errorCode}: {errStr}");
        }
    }

    [Test]
    public unsafe void TestVersionComparison()
    {
        // Test package version comparison
        IntPtr v1 = Marshal.StringToHGlobalAnsi("1.0.0");
        IntPtr v2 = Marshal.StringToHGlobalAnsi("1.0.1");
        IntPtr v3 = Marshal.StringToHGlobalAnsi("1.0.0");
        
        try
        {
            int cmp1 = NativeMethods.alpm_pkg_vercmp(
                (byte*)v1.ToPointer(),
                (byte*)v2.ToPointer());
            Assert.That(cmp1, Is.LessThan(0), "1.0.0 should be less than 1.0.1");
            
            int cmp2 = NativeMethods.alpm_pkg_vercmp(
                (byte*)v2.ToPointer(),
                (byte*)v1.ToPointer());
            Assert.That(cmp2, Is.GreaterThan(0), "1.0.1 should be greater than 1.0.0");
            
            int cmp3 = NativeMethods.alpm_pkg_vercmp(
                (byte*)v1.ToPointer(),
                (byte*)v3.ToPointer());
            Assert.That(cmp3, Is.EqualTo(0), "1.0.0 should be equal to 1.0.0");
            
            TestContext.WriteLine("Version comparison tests passed");
        }
        finally
        {
            Marshal.FreeHGlobal(v1);
            Marshal.FreeHGlobal(v2);
            Marshal.FreeHGlobal(v3);
        }
    }
}

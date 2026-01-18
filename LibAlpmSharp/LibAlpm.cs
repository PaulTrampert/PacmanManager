using System;
using System.Runtime.InteropServices;
using LibAlpmSharp.Interop;

namespace LibAlpmSharp;

/// <summary>
/// Represents a handle to the libalpm library.
/// This class provides safe, managed access to the Arch Linux Package Manager library.
/// </summary>
public sealed class LibAlpm : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    /// <summary>
    /// Gets the native handle to the libalpm context.
    /// </summary>
    internal IntPtr Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    /// <summary>
    /// Gets the root path for filesystem operations.
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// Gets the absolute path to the libalpm database.
    /// </summary>
    public string DbPath { get; }

    /// <summary>
    /// Private constructor. Use factory methods to create instances.
    /// </summary>
    /// <param name="handle">The native handle returned from alpm_initialize</param>
    /// <param name="root">The root path</param>
    /// <param name="dbPath">The database path</param>
    private LibAlpm(IntPtr handle, string root, string dbPath)
    {
        if (handle == IntPtr.Zero)
            throw new ArgumentNullException(nameof(handle));
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("Root path cannot be null or empty", nameof(root));
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("Database path cannot be null or empty", nameof(dbPath));

        _handle = handle;
        Root = root;
        DbPath = dbPath;
    }

    /// <summary>
    /// Initializes the libalpm library with the specified root and database paths.
    /// </summary>
    /// <param name="root">The root path for all filesystem operations. Typically "/".</param>
    /// <param name="dbPath">The absolute path to the libalpm database. Typically "/var/lib/pacman".</param>
    /// <returns>A new <see cref="LibAlpm"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when root or dbPath is null or empty.</exception>
    /// <exception cref="AlpmException">Thrown when libalpm initialization fails.</exception>
    public static LibAlpm Initialize(string root, string dbPath)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("Root path cannot be null or empty", nameof(root));
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("Database path cannot be null or empty", nameof(dbPath));

        IntPtr rootPtr = IntPtr.Zero;
        IntPtr dbPathPtr = IntPtr.Zero;

        try
        {
            rootPtr = Marshal.StringToHGlobalAnsi(root);
            dbPathPtr = Marshal.StringToHGlobalAnsi(dbPath);

            unsafe
            {
                AlpmErrno err;
                IntPtr handle = NativeMethods.alpm_initialize(
                    (byte*)rootPtr.ToPointer(),
                    (byte*)dbPathPtr.ToPointer(),
                    &err);

                if (handle == IntPtr.Zero)
                {
                    throw new AlpmException(err, $"Failed to initialize libalpm: {GetErrorString(err)}");
                }

                return new LibAlpm(handle, root, dbPath);
            }
        }
        finally
        {
            if (rootPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(rootPtr);
            if (dbPathPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(dbPathPtr);
        }
    }

    /// <summary>
    /// Initializes the libalpm library with default paths.
    /// Uses "/" as root and "/var/lib/pacman" as database path.
    /// </summary>
    /// <returns>A new <see cref="LibAlpm"/> instance.</returns>
    /// <exception cref="AlpmException">Thrown when libalpm initialization fails.</exception>
    public static LibAlpm Initialize()
    {
        return Initialize("/", "/var/lib/pacman");
    }

    /// <summary>
    /// Gets the current error code from the handle.
    /// </summary>
    /// <returns>The current error code.</returns>
    public AlpmErrno GetLastError()
    {
        ThrowIfDisposed();
        return NativeMethods.alpm_errno(_handle);
    }

    /// <summary>
    /// Gets the string corresponding to an error number.
    /// </summary>
    /// <param name="err">The error code.</param>
    /// <returns>A string describing the error.</returns>
    public static string GetErrorString(AlpmErrno err)
    {
        unsafe
        {
            byte* errPtr = NativeMethods.alpm_strerror(err);
            return Marshal.PtrToStringUTF8((IntPtr)errPtr) ?? "Unknown error";
        }
    }

    /// <summary>
    /// Gets the version of the libalpm library.
    /// </summary>
    /// <returns>The version string.</returns>
    public static string GetVersion()
    {
        unsafe
        {
            byte* versionPtr = NativeMethods.alpm_version();
            return Marshal.PtrToStringUTF8((IntPtr)versionPtr) ?? "Unknown";
        }
    }

    /// <summary>
    /// Gets the capabilities of the libalpm library.
    /// </summary>
    /// <returns>A bitmask of capabilities.</returns>
    public static AlpmCaps GetCapabilities()
    {
        return (AlpmCaps)NativeMethods.alpm_capabilities();
    }

    /// <summary>
    /// Throws <see cref="ObjectDisposedException"/> if this instance has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LibAlpm));
    }

    /// <summary>
    /// Releases all resources used by this <see cref="LibAlpm"/> instance.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.alpm_release(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer to ensure native resources are released.
    /// </summary>
    ~LibAlpm()
    {
        Dispose();
    }
}

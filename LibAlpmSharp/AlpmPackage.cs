using System;
using System.Runtime.InteropServices;
using LibAlpmSharp.Interop;

namespace LibAlpmSharp;

/// <summary>
/// Represents a package, either one belonging to a database or one loaded from a file by
/// <see cref="ILibAlpm.LoadPackageFile"/>.
/// </summary>
public sealed class AlpmPackage : IPackage
{
    private readonly bool _ownsHandle;
    private IntPtr _pkgHandle;
    private bool _disposed;

    /// <summary>
    /// Gets the package name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the package version.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets the package description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the native handle to the package.
    /// </summary>
    internal IntPtr Handle
    {
        get
        {
            ThrowIfDisposed();
            return _pkgHandle;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlpmPackage"/> class.
    /// </summary>
    /// <param name="pkgHandle">The native package handle.</param>
    /// <param name="ownsHandle">
    /// Whether this instance is responsible for freeing the handle. Packages obtained from a
    /// database are owned by that database and must not be freed; packages loaded from a file
    /// with <see cref="ILibAlpm.LoadPackageFile"/> are owned by the caller.
    /// </param>
    internal AlpmPackage(IntPtr pkgHandle, bool ownsHandle = false)
    {
        if (pkgHandle == IntPtr.Zero)
            throw new ArgumentException("Package handle cannot be null", nameof(pkgHandle));

        _pkgHandle = pkgHandle;
        _ownsHandle = ownsHandle;

        unsafe
        {
            byte* namePtr = NativeMethods.alpm_pkg_get_name(_pkgHandle);
            Name = Marshal.PtrToStringUTF8((IntPtr)namePtr) ?? string.Empty;

            byte* versionPtr = NativeMethods.alpm_pkg_get_version(_pkgHandle);
            Version = Marshal.PtrToStringUTF8((IntPtr)versionPtr) ?? string.Empty;

            byte* descPtr = NativeMethods.alpm_pkg_get_desc(_pkgHandle);
            Description = Marshal.PtrToStringUTF8((IntPtr)descPtr) ?? string.Empty;
        }
    }

    /// <summary>
    /// Returns a string that represents the current package.
    /// </summary>
    /// <returns>A string that represents the current package.</returns>
    public override string ToString()
    {
        return $"{Name} {Version}";
    }

    /// <summary>
    /// Gets the package base name.
    /// </summary>
    /// <returns>The base name of the package.</returns>
    public string GetBase()
    {
        unsafe
        {
            byte* basePtr = NativeMethods.alpm_pkg_get_base(Handle);
            return Marshal.PtrToStringUTF8((IntPtr)basePtr) ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets the package URL.
    /// </summary>
    /// <returns>The URL of the package.</returns>
    public string GetUrl()
    {
        unsafe
        {
            byte* urlPtr = NativeMethods.alpm_pkg_get_url(Handle);
            return Marshal.PtrToStringUTF8((IntPtr)urlPtr) ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets the architecture for which the package was built.
    /// </summary>
    /// <returns>The architecture string.</returns>
    public string GetArchitecture()
    {
        unsafe
        {
            byte* archPtr = NativeMethods.alpm_pkg_get_arch(Handle);
            return Marshal.PtrToStringUTF8((IntPtr)archPtr) ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets the packager's name.
    /// </summary>
    /// <returns>The packager's name.</returns>
    public string GetPackager()
    {
        unsafe
        {
            byte* packagerPtr = NativeMethods.alpm_pkg_get_packager(Handle);
            return Marshal.PtrToStringUTF8((IntPtr)packagerPtr) ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets the installed size of the package.
    /// </summary>
    /// <returns>The installed size in bytes.</returns>
    public long GetInstalledSize()
    {
        return NativeMethods.alpm_pkg_get_isize(Handle);
    }

    /// <summary>
    /// Gets the download size of the package.
    /// </summary>
    /// <returns>The download size in bytes.</returns>
    public long GetDownloadSize()
    {
        return NativeMethods.alpm_pkg_get_size(Handle);
    }

    /// <summary>
    /// Gets the build date of the package.
    /// </summary>
    /// <returns>The build date.</returns>
    public DateTimeOffset GetBuildDate()
    {
        var timestamp = NativeMethods.alpm_pkg_get_builddate(Handle);
        return DateTimeOffset.FromUnixTimeSeconds(timestamp);
    }

    /// <summary>
    /// Gets the install date of the package.
    /// </summary>
    /// <returns>The install date, or null if not installed.</returns>
    public DateTimeOffset? GetInstallDate()
    {
        var timestamp = NativeMethods.alpm_pkg_get_installdate(Handle);
        return DateTimeOffset.FromUnixTimeSeconds(timestamp);
    }

    /// <summary>
    /// Gets the list of package dependencies.
    /// </summary>
    /// <returns>A list of dependencies.</returns>
    public List<AlpmDependency> GetDependencies()
    {
        var dependencies = new List<AlpmDependency>();
        
        unsafe
        {
            AlpmList* depList = NativeMethods.alpm_pkg_get_depends(Handle);
            AlpmList* current = depList;

            while (current != null)
            {
                if (current->data != null)
                {
                    var depend = (AlpmDepend*)current->data;
                    dependencies.Add(new AlpmDependency(depend));
                }
                current = NativeMethods.alpm_list_next(current);
            }
        }

        return dependencies;
    }

    /// <summary>
    /// Gets the list of package optional dependencies.
    /// </summary>
    /// <returns>A list of optional dependencies.</returns>
    public List<AlpmDependency> GetOptionalDependencies()
    {
        var dependencies = new List<AlpmDependency>();
        
        unsafe
        {
            AlpmList* depList = NativeMethods.alpm_pkg_get_optdepends(Handle);
            AlpmList* current = depList;

            while (current != null)
            {
                if (current->data != null)
                {
                    var depend = (AlpmDepend*)current->data;
                    dependencies.Add(new AlpmDependency(depend));
                }
                current = NativeMethods.alpm_list_next(current);
            }
        }

        return dependencies;
    }

    /// <summary>
    /// Computes the list of packages requiring this package.
    /// Note: The returned list must be freed by the caller.
    /// </summary>
    /// <returns>A list of package names that require this package.</returns>
    public List<string> GetRequiredBy()
    {
        var requiredBy = new List<string>();
        
        unsafe
        {
            AlpmList* list = NativeMethods.alpm_pkg_compute_requiredby(Handle);
            AlpmList* current = list;

            try
            {
                while (current != null)
                {
                    if (current->data != null)
                    {
                        string? pkgName = Marshal.PtrToStringUTF8((IntPtr)current->data);
                        if (!string.IsNullOrEmpty(pkgName))
                            requiredBy.Add(pkgName);
                    }
                    current = NativeMethods.alpm_list_next(current);
                }
            }
            finally
            {
                // Free the list - the compute functions allocate a new list that we own
                if (list != null)
                {
                    AlpmListFnFree freeStringDelegate = NativeMethods.free;
                    NativeMethods.alpm_list_free_inner(list, freeStringDelegate);
                    NativeMethods.alpm_list_free(list);
                }
            }
        }

        return requiredBy;
    }

    /// <summary>
    /// Computes the list of packages that optionally require this package.
    /// Note: The returned list must be freed by the caller.
    /// </summary>
    /// <returns>A list of package names that optionally require this package.</returns>
    public List<string> GetOptionalFor()
    {
        var optionalFor = new List<string>();
        
        unsafe
        {
            AlpmList* list = NativeMethods.alpm_pkg_compute_optionalfor(Handle);
            AlpmList* current = list;

            try
            {
                while (current != null)
                {
                    if (current->data != null)
                    {
                        string? pkgName = Marshal.PtrToStringUTF8((IntPtr)current->data);
                        if (!string.IsNullOrEmpty(pkgName))
                            optionalFor.Add(pkgName);
                    }
                    current = NativeMethods.alpm_list_next(current);
                }
            }
            finally
            {
                // Free the list - the compute functions allocate a new list that we own
                if (list != null)
                {
                    AlpmListFnFree freeStringDelegate = NativeMethods.free;
                    NativeMethods.alpm_list_free_inner(list, freeStringDelegate);
                    NativeMethods.alpm_list_free(list);
                }
            }
        }

        return optionalFor;
    }

    /// <summary>
    /// Gets the list of packages that conflict with this package.
    /// </summary>
    /// <returns>A list of conflicting package dependencies.</returns>
    public List<AlpmDependency> GetConflicts()
    {
        var conflicts = new List<AlpmDependency>();
        
        unsafe
        {
            AlpmList* conflictList = NativeMethods.alpm_pkg_get_conflicts(Handle);
            AlpmList* current = conflictList;

            while (current != null)
            {
                if (current->data != null)
                {
                    var depend = (AlpmDepend*)current->data;
                    conflicts.Add(new AlpmDependency(depend));
                }
                current = NativeMethods.alpm_list_next(current);
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Throws <see cref="ObjectDisposedException"/> if this instance has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AlpmPackage));
    }

    /// <summary>
    /// Releases the native package handle if this instance owns it. Disposing a package that
    /// belongs to a database does nothing, since the database owns the handle.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        if (_ownsHandle && _pkgHandle != IntPtr.Zero)
        {
            NativeMethods.alpm_pkg_free(_pkgHandle);
        }

        _pkgHandle = IntPtr.Zero;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer to ensure an owned native handle is released.
    /// </summary>
    ~AlpmPackage()
    {
        Dispose();
    }
}

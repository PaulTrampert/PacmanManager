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
    /// <remarks>
    /// libalpm reports a zero timestamp for a package that is not installed — every package loaded
    /// from a file, for instance — which this returns as <see langword="null"/> rather than as the
    /// Unix epoch.
    /// </remarks>
    /// <returns>The install date, or <see langword="null"/> if the package is not installed.</returns>
    public DateTimeOffset? GetInstallDate()
    {
        var timestamp = NativeMethods.alpm_pkg_get_installdate(Handle);
        return timestamp == 0 ? null : DateTimeOffset.FromUnixTimeSeconds(timestamp);
    }

    /// <summary>
    /// Gets the name of the file the package was loaded from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is not a basename. For a package loaded with <see cref="ILibAlpm.LoadPackageFile"/>
    /// libalpm reports back the path it was handed, so a package read out of a temporary directory
    /// reports that temporary path.
    /// </para>
    /// <para>
    /// <b>Nothing may use this to name a stored file.</b> The packages API derives the name it
    /// stores a package under from the package's own metadata. This member is bound for
    /// completeness only.
    /// </para>
    /// </remarks>
    /// <returns>The file name, or <see langword="null"/> if the package did not come from a file.</returns>
    public string? GetFileName()
    {
        unsafe
        {
            byte* fileNamePtr = NativeMethods.alpm_pkg_get_filename(Handle);
            return Marshal.PtrToStringUTF8((IntPtr)fileNamePtr);
        }
    }

    /// <summary>
    /// Gets the package's SHA256 checksum.
    /// </summary>
    /// <remarks>
    /// libalpm populates this from a sync database entry, so it is <see langword="null"/> for a
    /// package loaded from a file with <see cref="ILibAlpm.LoadPackageFile"/>. Callers that need a
    /// checksum of an uploaded file must compute it themselves over the bytes they received.
    /// </remarks>
    /// <returns>The 64 lowercase hexadecimal digit checksum, or <see langword="null"/> if libalpm has none.</returns>
    public string? GetSha256Sum()
    {
        unsafe
        {
            byte* sumPtr = NativeMethods.alpm_pkg_get_sha256sum(Handle);
            return Marshal.PtrToStringUTF8((IntPtr)sumPtr);
        }
    }

    /// <summary>
    /// Gets the package's MD5 checksum.
    /// </summary>
    /// <remarks>
    /// libalpm populates this from a sync database entry, so it is <see langword="null"/> for a
    /// package loaded from a file with <see cref="ILibAlpm.LoadPackageFile"/>. Callers that need a
    /// checksum of an uploaded file must compute it themselves over the bytes they received.
    /// </remarks>
    /// <returns>The 32 lowercase hexadecimal digit checksum, or <see langword="null"/> if libalpm has none.</returns>
    public string? GetMd5Sum()
    {
        unsafe
        {
            byte* sumPtr = NativeMethods.alpm_pkg_get_md5sum(Handle);
            return Marshal.PtrToStringUTF8((IntPtr)sumPtr);
        }
    }

    /// <summary>
    /// Gets the licenses the package is distributed under.
    /// </summary>
    /// <returns>A list of license identifiers, empty if the package declares none.</returns>
    public List<string> GetLicenses()
    {
        unsafe
        {
            return ReadStringList(NativeMethods.alpm_pkg_get_licenses(Handle));
        }
    }

    /// <summary>
    /// Gets the groups the package belongs to.
    /// </summary>
    /// <returns>A list of group names, empty if the package belongs to none.</returns>
    public List<string> GetGroups()
    {
        unsafe
        {
            return ReadStringList(NativeMethods.alpm_pkg_get_groups(Handle));
        }
    }

    /// <summary>
    /// Gets the list of package dependencies.
    /// </summary>
    /// <returns>A list of dependencies.</returns>
    public List<AlpmDependency> GetDependencies()
    {
        unsafe
        {
            return ReadDependencyList(NativeMethods.alpm_pkg_get_depends(Handle));
        }
    }

    /// <summary>
    /// Gets the list of package optional dependencies.
    /// </summary>
    /// <returns>A list of optional dependencies.</returns>
    public List<AlpmDependency> GetOptionalDependencies()
    {
        unsafe
        {
            return ReadDependencyList(NativeMethods.alpm_pkg_get_optdepends(Handle));
        }
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
        unsafe
        {
            return ReadDependencyList(NativeMethods.alpm_pkg_get_conflicts(Handle));
        }
    }

    /// <summary>
    /// Gets the list of virtual packages this package provides.
    /// </summary>
    /// <returns>A list of provisions, each of which may carry a version.</returns>
    public List<AlpmDependency> GetProvides()
    {
        unsafe
        {
            return ReadDependencyList(NativeMethods.alpm_pkg_get_provides(Handle));
        }
    }

    /// <summary>
    /// Gets the list of packages this package replaces.
    /// </summary>
    /// <returns>A list of replaced packages.</returns>
    public List<AlpmDependency> GetReplaces()
    {
        unsafe
        {
            return ReadDependencyList(NativeMethods.alpm_pkg_get_replaces(Handle));
        }
    }

    /// <summary>
    /// Gets the list of dependencies required to build the package.
    /// </summary>
    /// <returns>A list of make dependencies.</returns>
    public List<AlpmDependency> GetMakeDepends()
    {
        unsafe
        {
            return ReadDependencyList(NativeMethods.alpm_pkg_get_makedepends(Handle));
        }
    }

    /// <summary>
    /// Gets the list of dependencies required to run the package's test suite.
    /// </summary>
    /// <returns>A list of check dependencies.</returns>
    public List<AlpmDependency> GetCheckDepends()
    {
        unsafe
        {
            return ReadDependencyList(NativeMethods.alpm_pkg_get_checkdepends(Handle));
        }
    }

    /// <summary>
    /// Reads a libalpm list of <c>alpm_depend_t</c> into managed dependencies. The list belongs to
    /// the package, so it is walked but never freed.
    /// </summary>
    /// <param name="list">The head of the native list, which may be null for an empty list.</param>
    /// <returns>The dependencies the list held.</returns>
    private static unsafe List<AlpmDependency> ReadDependencyList(AlpmList* list)
    {
        var dependencies = new List<AlpmDependency>();

        for (AlpmList* current = list; current != null; current = NativeMethods.alpm_list_next(current))
        {
            if (current->data != null)
                dependencies.Add(new AlpmDependency((AlpmDepend*)current->data));
        }

        return dependencies;
    }

    /// <summary>
    /// Reads a libalpm list of strings into a managed list. The list belongs to the package, so it
    /// is walked but never freed.
    /// </summary>
    /// <param name="list">The head of the native list, which may be null for an empty list.</param>
    /// <returns>The strings the list held.</returns>
    private static unsafe List<string> ReadStringList(AlpmList* list)
    {
        var values = new List<string>();

        for (AlpmList* current = list; current != null; current = NativeMethods.alpm_list_next(current))
        {
            string? value = Marshal.PtrToStringUTF8((IntPtr)current->data);
            if (!string.IsNullOrEmpty(value))
                values.Add(value);
        }

        return values;
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

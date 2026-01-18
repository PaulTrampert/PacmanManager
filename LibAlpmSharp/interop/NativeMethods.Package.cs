using System;
using System.Runtime.InteropServices;

namespace LibAlpmSharp.Interop;

/// <summary>
/// Native methods for libalpm - Package and Transaction functions
/// </summary>
internal static unsafe partial class NativeMethods
{
    // ============================================================================
    // alpm.h - Package functions
    // ============================================================================

    /// <summary>Create a package from a file</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_pkg_load(IntPtr handle, byte* filename, int full, int level, IntPtr* pkg);

    /// <summary>Free a package</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_pkg_free(IntPtr pkg);

    /// <summary>Check the integrity (with md5) of a package from the sync cache</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_pkg_checkmd5sum(IntPtr pkg);

    /// <summary>Compare two version strings</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_pkg_vercmp(byte* a, byte* b);

    /// <summary>Computes the list of packages requiring a given package</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_pkg_compute_requiredby(IntPtr pkg);

    /// <summary>Computes the list of packages optionally requiring a given package</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_pkg_compute_optionalfor(IntPtr pkg);

    /// <summary>Test if a package should be ignored</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_pkg_should_ignore(IntPtr handle, IntPtr pkg);

    /// <summary>Gets the handle of a package</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr alpm_pkg_get_handle(IntPtr pkg);

    /// <summary>Gets the name of the file from which the package was loaded</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_pkg_get_filename(IntPtr pkg);

    /// <summary>Returns the package base name</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_pkg_get_base(IntPtr pkg);

    /// <summary>Returns the package name</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_pkg_get_name(IntPtr pkg);

    /// <summary>Returns the package version as a string</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_pkg_get_version(IntPtr pkg);

    /// <summary>Returns the origin of the package</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmPkgFrom alpm_pkg_get_origin(IntPtr pkg);

    /// <summary>Returns the package description</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_pkg_get_desc(IntPtr pkg);

    /// <summary>Returns the package URL</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_pkg_get_url(IntPtr pkg);

    /// <summary>Returns the build timestamp of the package</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern long alpm_pkg_get_builddate(IntPtr pkg);

    /// <summary>Returns the install timestamp of the package</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern long alpm_pkg_get_installdate(IntPtr pkg);

    /// <summary>Returns the packager's name</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_pkg_get_packager(IntPtr pkg);

    /// <summary>Returns the package's MD5 checksum as a string</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_pkg_get_md5sum(IntPtr pkg);

    /// <summary>Returns the package's SHA256 checksum as a string</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_pkg_get_sha256sum(IntPtr pkg);

    /// <summary>Returns the architecture for which the package was built</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_pkg_get_arch(IntPtr pkg);

    /// <summary>Returns the size of the package</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern long alpm_pkg_get_size(IntPtr pkg);

    /// <summary>Returns the installed size of the package</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern long alpm_pkg_get_isize(IntPtr pkg);

    /// <summary>Returns the package installation reason</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmPkgReason alpm_pkg_get_reason(IntPtr pkg);

    /// <summary>Returns the list of package licenses</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_pkg_get_licenses(IntPtr pkg);

    /// <summary>Returns the list of package groups</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_pkg_get_groups(IntPtr pkg);

    /// <summary>Returns the list of package dependencies</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_pkg_get_depends(IntPtr pkg);

    /// <summary>Returns the list of package optional dependencies</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_pkg_get_optdepends(IntPtr pkg);

    /// <summary>Returns a list of package check dependencies</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_pkg_get_checkdepends(IntPtr pkg);

    /// <summary>Returns a list of package make dependencies</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_pkg_get_makedepends(IntPtr pkg);

    /// <summary>Returns the list of packages conflicting with pkg</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_pkg_get_conflicts(IntPtr pkg);

    /// <summary>Returns the list of packages provided by pkg</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_pkg_get_provides(IntPtr pkg);

    /// <summary>Returns the list of packages to be replaced by pkg</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_pkg_get_replaces(IntPtr pkg);

    /// <summary>Returns the list of files installed by pkg</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmFilelist* alpm_pkg_get_files(IntPtr pkg);

    /// <summary>Returns the list of files backed up when installing pkg</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_pkg_get_backup(IntPtr pkg);

    /// <summary>Returns the database containing pkg</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr alpm_pkg_get_db(IntPtr pkg);

    /// <summary>Returns the base64 encoded package signature</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_pkg_get_base64_sig(IntPtr pkg);

    /// <summary>Extracts package signature either from embedded package signature or detached signature file</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_pkg_get_sig(IntPtr pkg, byte** sig, nuint* sig_len);

    /// <summary>Returns the method used to validate a package during install</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_pkg_get_validation(IntPtr pkg);

    /// <summary>Gets the extended data field of a package</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_pkg_get_xdata(IntPtr pkg);

    /// <summary>Returns whether the package has an install scriptlet</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_pkg_has_scriptlet(IntPtr pkg);

    /// <summary>Returns the size of the files that will be downloaded to install a package</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern long alpm_pkg_download_size(IntPtr newpkg);

    /// <summary>Set install reason for a package in the local database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_pkg_set_reason(IntPtr pkg, AlpmPkgReason reason);

    /// <summary>Open a package changelog for reading</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void* alpm_pkg_changelog_open(IntPtr pkg);

    /// <summary>Read data from an open changelog 'file stream'</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint alpm_pkg_changelog_read(void* ptr, nuint size, IntPtr pkg, void* fp);

    /// <summary>Close a package changelog for reading</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_pkg_changelog_close(IntPtr pkg, void* fp);

    /// <summary>Open a package mtree file for reading</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr alpm_pkg_mtree_open(IntPtr pkg);

    /// <summary>Read next entry from a package mtree file</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_pkg_mtree_next(IntPtr pkg, IntPtr archive, IntPtr* entry);

    /// <summary>Close a package mtree file</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_pkg_mtree_close(IntPtr pkg, IntPtr archive);

    // ============================================================================
    // alpm.h - Transaction functions
    // ============================================================================

    /// <summary>Returns the bitfield of flags for the current transaction</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_trans_get_flags(IntPtr handle);

    /// <summary>Returns a list of packages added by the transaction</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_trans_get_add(IntPtr handle);

    /// <summary>Returns the list of packages removed by the transaction</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_trans_get_remove(IntPtr handle);

    /// <summary>Initialize the transaction</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_trans_init(IntPtr handle, int flags);

    /// <summary>Prepare a transaction</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_trans_prepare(IntPtr handle, AlpmList** data);

    /// <summary>Commit a transaction</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_trans_commit(IntPtr handle, AlpmList** data);

    /// <summary>Interrupt a transaction</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_trans_interrupt(IntPtr handle);

    /// <summary>Release a transaction</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_trans_release(IntPtr handle);

    /// <summary>Search for packages to upgrade and add them to the transaction</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_sync_sysupgrade(IntPtr handle, int enable_downgrade);

    /// <summary>Add a package to the transaction</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_add_pkg(IntPtr handle, IntPtr pkg);

    /// <summary>Add a package removal to the transaction</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_remove_pkg(IntPtr handle, IntPtr pkg);

    // ============================================================================
    // alpm.h - Miscellaneous functions
    // ============================================================================

    /// <summary>Check for new version of pkg in syncdbs</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr alpm_sync_get_new_version(IntPtr pkg, AlpmList* dbs_sync);

    /// <summary>Get the md5 sum of file</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_compute_md5sum(byte* filename);

    /// <summary>Get the sha256 sum of file</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_compute_sha256sum(byte* filename);

    /// <summary>Remove the database lock file</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_unlock(IntPtr handle);

    /// <summary>Get the version of library</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_version();

    /// <summary>Get the capabilities of the library</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_capabilities();

    /// <summary>Drop privileges by switching to a different user</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_sandbox_setup_child(IntPtr handle, byte* sandboxuser, byte* sandbox_path, bool restrict_syscalls);
}

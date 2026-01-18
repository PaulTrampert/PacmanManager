using System;
using System.Runtime.InteropServices;

namespace LibAlpmSharp.Interop;

/// <summary>
/// Native methods for libalpm (alpm_list.h)
/// </summary>
internal static unsafe partial class NativeMethods
{
    private const string LibAlpm = "libalpm.so";

    // ============================================================================
    // alpm_list.h - List manipulation functions
    // ============================================================================

    /// <summary>Free a list, but not the contained data</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void alpm_list_free(AlpmList* list);

    /// <summary>Free the internal data of a list structure but not the list itself</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void alpm_list_free_inner(AlpmList* list, AlpmListFnFree fn);

    /// <summary>Add a new item to the end of the list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_add(AlpmList* list, void* data);

    /// <summary>Add a new item to the end of the list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_append(AlpmList** list, void* data);

    /// <summary>Duplicate and append a string to a list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_append_strdup(AlpmList** list, byte* data);

    /// <summary>Add items to a list in sorted order</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_add_sorted(AlpmList* list, void* data, AlpmListFnCmp fn);

    /// <summary>Join two lists</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_join(AlpmList* first, AlpmList* second);

    /// <summary>Merge the two sorted sublists into one sorted list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_mmerge(AlpmList* left, AlpmList* right, AlpmListFnCmp fn);

    /// <summary>Sort a list of size n using mergesort algorithm</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_msort(AlpmList* list, nuint n, AlpmListFnCmp fn);

    /// <summary>Remove an item from the list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_remove_item(AlpmList* haystack, AlpmList* item);

    /// <summary>Remove an item from the list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_remove(AlpmList* haystack, void* needle, AlpmListFnCmp fn, void** data);

    /// <summary>Remove a string from a list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_remove_str(AlpmList* haystack, byte* needle, byte** data);

    /// <summary>Create a new list without any duplicates</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_remove_dupes(AlpmList* list);

    /// <summary>Copy a string list, including data</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_strdup(AlpmList* list);

    /// <summary>Copy a list, without copying data</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_copy(AlpmList* list);

    /// <summary>Copy a list and copy the data</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_copy_data(AlpmList* list, nuint size);

    /// <summary>Create a new list in reverse order</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_reverse(AlpmList* list);

    /// <summary>Return nth element from list (starting from 0)</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_nth(AlpmList* list, nuint n);

    /// <summary>Get the next element of a list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_next(AlpmList* list);

    /// <summary>Get the previous element of a list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_previous(AlpmList* list);

    /// <summary>Get the last item in the list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_last(AlpmList* list);

    /// <summary>Get the number of items in a list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint alpm_list_count(AlpmList* list);

    /// <summary>Find an item in a list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void* alpm_list_find(AlpmList* haystack, void* needle, AlpmListFnCmp fn);

    /// <summary>Find an item in a list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void* alpm_list_find_ptr(AlpmList* haystack, void* needle);

    /// <summary>Find a string in a list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_list_find_str(AlpmList* haystack, byte* needle);

    /// <summary>Check if two lists contain the same data, ignoring order</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_list_cmp_unsorted(AlpmList* left, AlpmList* right, AlpmListFnCmp fn);

    /// <summary>Find the differences between list left and list right</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void alpm_list_diff_sorted(AlpmList* left, AlpmList* right, AlpmListFnCmp fn, AlpmList** onlyleft, AlpmList** onlyright);

    /// <summary>Find the items in list lhs that are not present in list rhs</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_list_diff(AlpmList* lhs, AlpmList* rhs, AlpmListFnCmp fn);

    /// <summary>Copy a list and data into a standard C array of fixed length</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void* alpm_list_to_array(AlpmList* list, nuint n, nuint size);

    // ============================================================================
    // alpm.h - Error handling functions
    // ============================================================================

    /// <summary>Returns the current error code from the handle</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmErrno alpm_errno(IntPtr handle);

    /// <summary>Returns the string corresponding to an error number</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_strerror(AlpmErrno err);

    // ============================================================================
    // alpm.h - Handle functions
    // ============================================================================

    /// <summary>Initializes the library</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr alpm_initialize(byte* root, byte* dbpath, AlpmErrno* err);

    /// <summary>Release the library</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_release(IntPtr handle);

    // ============================================================================
    // alpm.h - Signature checking functions
    // ============================================================================

    /// <summary>Check the PGP signature for the given package file</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_pkg_check_pgp_signature(IntPtr pkg, AlpmSigList* siglist);

    /// <summary>Check the PGP signature for the given database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_db_check_pgp_signature(IntPtr db, AlpmSigList* siglist);

    /// <summary>Clean up and free a signature result list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_siglist_cleanup(AlpmSigList* siglist);

    /// <summary>Decode a loaded signature in base64 form</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_decode_signature(byte* base64_data, byte** data, nuint* data_len);

    /// <summary>Extract the Issuer Key ID from a signature</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_extract_keyid(IntPtr handle, byte* identifier, byte* sig, nuint len, AlpmList** keys);

    // ============================================================================
    // alpm.h - Dependency functions
    // ============================================================================

    /// <summary>Checks dependencies and returns missing ones in a list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_checkdeps(IntPtr handle, AlpmList* pkglist, AlpmList* remove, AlpmList* upgrade, int reversedeps);

    /// <summary>Find a package satisfying a specified dependency</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr alpm_find_satisfier(AlpmList* pkgs, byte* depstring);

    /// <summary>Find a package satisfying a specified dependency</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr alpm_find_dbs_satisfier(IntPtr handle, AlpmList* dbs, byte* depstring);

    /// <summary>Check the package conflicts in a database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_checkconflicts(IntPtr handle, AlpmList* pkglist);

    /// <summary>Returns a newly allocated string representing the dependency information</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_dep_compute_string(AlpmDepend* dep);

    /// <summary>Return a newly allocated dependency information parsed from a string</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmDepend* alpm_dep_from_string(byte* depstring);

    /// <summary>Free a dependency info structure</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void alpm_dep_free(AlpmDepend* dep);

    /// <summary>Free a fileconflict and its members</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void alpm_fileconflict_free(AlpmFileConflict* conflict);

    /// <summary>Free a depmissing and its members</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void alpm_depmissing_free(AlpmDepMissing* miss);

    /// <summary>Free a conflict and its members</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void alpm_conflict_free(AlpmConflict* conflict);

    // ============================================================================
    // alpm.h - File functions
    // ============================================================================

    /// <summary>Determines whether a package filelist contains a given path</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmFile* alpm_filelist_contains(AlpmFilelist* filelist, byte* path);

    // ============================================================================
    // alpm.h - Group functions
    // ============================================================================

    /// <summary>Find group members across a list of databases</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_find_group_pkgs(AlpmList* dbs, byte* name);

    // ============================================================================
    // alpm.h - Database functions
    // ============================================================================

    /// <summary>Get the database of locally installed packages</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr alpm_get_localdb(IntPtr handle);

    /// <summary>Get the list of sync databases</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_get_syncdbs(IntPtr handle);

    /// <summary>Register a sync database of packages</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr alpm_register_syncdb(IntPtr handle, byte* treename, int level);

    /// <summary>Unregister all package databases</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_unregister_all_syncdbs(IntPtr handle);

    /// <summary>Unregister a package database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_db_unregister(IntPtr db);

    /// <summary>Get the handle of a package database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr alpm_db_get_handle(IntPtr db);

    /// <summary>Get the name of a package database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_db_get_name(IntPtr db);

    /// <summary>Get the signature verification level for a database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_db_get_siglevel(IntPtr db);

    /// <summary>Check the validity of a database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_db_get_valid(IntPtr db);

    /// <summary>Get the list of servers assigned to this db</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_db_get_servers(IntPtr db);

    /// <summary>Sets the list of servers for the database to use</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_db_set_servers(IntPtr db, AlpmList* servers);

    /// <summary>Add a download server to a database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_db_add_server(IntPtr db, byte* url);

    /// <summary>Remove a download server from a database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_db_remove_server(IntPtr db, byte* url);

    /// <summary>Get the list of cache servers assigned to this db</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_db_get_cache_servers(IntPtr db);

    /// <summary>Sets the list of cache servers for the database to use</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_db_set_cache_servers(IntPtr db, AlpmList* servers);

    /// <summary>Add a download cache server to a database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_db_add_cache_server(IntPtr db, byte* url);

    /// <summary>Remove a download cache server from a database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_db_remove_cache_server(IntPtr db, byte* url);

    /// <summary>Update package databases</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_db_update(IntPtr handle, AlpmList* dbs, int force);

    /// <summary>Get a package entry from a package database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr alpm_db_get_pkg(IntPtr db, byte* name);

    /// <summary>Get the package cache of a package database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_db_get_pkgcache(IntPtr db);

    /// <summary>Get a group entry from a package database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmGroup* alpm_db_get_group(IntPtr db, byte* name);

    /// <summary>Get the group cache of a package database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_db_get_groupcache(IntPtr db);

    /// <summary>Searches a database with regular expressions</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_db_search(IntPtr db, AlpmList* needles, AlpmList** ret);

    /// <summary>Sets the usage of a database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_db_set_usage(IntPtr db, int usage);

    /// <summary>Gets the usage of a database</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_db_get_usage(IntPtr db, int* usage);

    // ============================================================================
    // alpm.h - Options/Callbacks functions
    // ============================================================================

    /// <summary>Returns the callback used for logging</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmCbLog alpm_option_get_logcb(IntPtr handle);

    /// <summary>Returns the callback used for logging context</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void* alpm_option_get_logcb_ctx(IntPtr handle);

    /// <summary>Sets the callback used for logging</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_logcb(IntPtr handle, AlpmCbLog cb, void* ctx);

    /// <summary>Returns the callback used to report download progress</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmCbDownload alpm_option_get_dlcb(IntPtr handle);

    /// <summary>Returns the callback used to report download progress context</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void* alpm_option_get_dlcb_ctx(IntPtr handle);

    /// <summary>Sets the callback used to report download progress</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_dlcb(IntPtr handle, AlpmCbDownload cb, void* ctx);

    /// <summary>Returns the downloading callback</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmCbFetch alpm_option_get_fetchcb(IntPtr handle);

    /// <summary>Returns the downloading callback context</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void* alpm_option_get_fetchcb_ctx(IntPtr handle);

    /// <summary>Sets the downloading callback</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_fetchcb(IntPtr handle, AlpmCbFetch cb, void* ctx);

    /// <summary>Returns the callback used for events</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmCbEvent alpm_option_get_eventcb(IntPtr handle);

    /// <summary>Returns the callback used for events context</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void* alpm_option_get_eventcb_ctx(IntPtr handle);

    /// <summary>Sets the callback used for events</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_eventcb(IntPtr handle, AlpmCbEvent cb, void* ctx);

    /// <summary>Returns the callback used for questions</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmCbQuestion alpm_option_get_questioncb(IntPtr handle);

    /// <summary>Returns the callback used for questions context</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void* alpm_option_get_questioncb_ctx(IntPtr handle);

    /// <summary>Sets the callback used for questions</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_questioncb(IntPtr handle, AlpmCbQuestion cb, void* ctx);

    /// <summary>Returns the callback used for operation progress</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmCbProgress alpm_option_get_progresscb(IntPtr handle);

    /// <summary>Returns the callback used for operation progress context</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern void* alpm_option_get_progresscb_ctx(IntPtr handle);

    /// <summary>Sets the callback used for operation progress</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_progresscb(IntPtr handle, AlpmCbProgress cb, void* ctx);

    /// <summary>Returns the root path</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_option_get_root(IntPtr handle);

    /// <summary>Returns the path to the database directory</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_option_get_dbpath(IntPtr handle);

    /// <summary>Get the name of the database lock file</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_option_get_lockfile(IntPtr handle);

    /// <summary>Gets the currently configured cachedirs</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_option_get_cachedirs(IntPtr handle);

    /// <summary>Sets the cachedirs</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_cachedirs(IntPtr handle, AlpmList* cachedirs);

    /// <summary>Append a cachedir to the configured cachedirs</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_add_cachedir(IntPtr handle, byte* cachedir);

    /// <summary>Remove a cachedir from the configured cachedirs</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_remove_cachedir(IntPtr handle, byte* cachedir);

    /// <summary>Gets the currently configured hookdirs</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_option_get_hookdirs(IntPtr handle);

    /// <summary>Sets the hookdirs</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_hookdirs(IntPtr handle, AlpmList* hookdirs);

    /// <summary>Append a hookdir to the configured hookdirs</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_add_hookdir(IntPtr handle, byte* hookdir);

    /// <summary>Remove a hookdir from the configured hookdirs</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_remove_hookdir(IntPtr handle, byte* hookdir);

    /// <summary>Gets the currently configured overwritable files</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_option_get_overwrite_files(IntPtr handle);

    /// <summary>Sets the overwritable files</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_overwrite_files(IntPtr handle, AlpmList* globs);

    /// <summary>Append an overwritable file to the configured overwritable files</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_add_overwrite_file(IntPtr handle, byte* glob);

    /// <summary>Remove a file glob from the configured overwritable files globs</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_remove_overwrite_file(IntPtr handle, byte* glob);

    /// <summary>Gets the filepath to the currently set logfile</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_option_get_logfile(IntPtr handle);

    /// <summary>Sets the logfile path</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_logfile(IntPtr handle, byte* logfile);

    /// <summary>Returns the path to libalpm's GnuPG home directory</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_option_get_gpgdir(IntPtr handle);

    /// <summary>Sets the path to libalpm's GnuPG home directory</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_gpgdir(IntPtr handle, byte* gpgdir);

    /// <summary>Returns the user to switch to for sensitive operations</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* alpm_option_get_sandboxuser(IntPtr handle);

    /// <summary>Sets the user to switch to for sensitive operations</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_sandboxuser(IntPtr handle, byte* sandboxuser);

    /// <summary>Returns whether to use syslog</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_get_usesyslog(IntPtr handle);

    /// <summary>Sets whether to use syslog</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_usesyslog(IntPtr handle, int usesyslog);

    /// <summary>Get the list of no-upgrade files</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_option_get_noupgrades(IntPtr handle);

    /// <summary>Add a file to the no-upgrade list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_add_noupgrade(IntPtr handle, byte* path);

    /// <summary>Sets the list of no-upgrade files</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_noupgrades(IntPtr handle, AlpmList* noupgrade);

    /// <summary>Remove an entry from the no-upgrade list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_remove_noupgrade(IntPtr handle, byte* path);

    /// <summary>Test if a path matches any of the globs in the no-upgrade list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_match_noupgrade(IntPtr handle, byte* path);

    /// <summary>Get the list of no-extract files</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_option_get_noextracts(IntPtr handle);

    /// <summary>Add a file to the no-extract list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_add_noextract(IntPtr handle, byte* path);

    /// <summary>Sets the list of no-extract files</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_noextracts(IntPtr handle, AlpmList* noextract);

    /// <summary>Remove an entry from the no-extract list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_remove_noextract(IntPtr handle, byte* path);

    /// <summary>Test if a path matches any of the globs in the no-extract list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_match_noextract(IntPtr handle, byte* path);

    /// <summary>Get the list of ignored packages</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_option_get_ignorepkgs(IntPtr handle);

    /// <summary>Add a package to the ignored packages list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_add_ignorepkg(IntPtr handle, byte* pkg);

    /// <summary>Sets the list of ignored packages</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_ignorepkgs(IntPtr handle, AlpmList* ignorepkgs);

    /// <summary>Remove a package from the ignored packages list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_remove_ignorepkg(IntPtr handle, byte* pkg);

    /// <summary>Get the list of ignored groups</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_option_get_ignoregroups(IntPtr handle);

    /// <summary>Add a group to the ignored groups list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_add_ignoregroup(IntPtr handle, byte* grp);

    /// <summary>Sets the list of ignored groups</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_ignoregroups(IntPtr handle, AlpmList* ignoregroups);

    /// <summary>Remove a group from the ignored groups list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_remove_ignoregroup(IntPtr handle, byte* grp);

    /// <summary>Get the targeted architectures</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern AlpmList* alpm_option_get_architectures(IntPtr handle);

    /// <summary>Add a architecture to the architectures list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_add_architecture(IntPtr handle, byte* arch);

    /// <summary>Sets the list of architectures</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_architectures(IntPtr handle, AlpmList* architectures);

    /// <summary>Remove an architecture from the architectures list</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_remove_architecture(IntPtr handle, byte* arch);

    /// <summary>Get whether to check disk space before installing packages</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_get_checkspace(IntPtr handle);

    /// <summary>Set whether to check disk space before installing packages</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_checkspace(IntPtr handle, int checkspace);

    /// <summary>Get the default siglevel</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_get_default_siglevel(IntPtr handle);

    /// <summary>Set the default siglevel</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_default_siglevel(IntPtr handle, int level);

    /// <summary>Get the default local file siglevel</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_get_local_file_siglevel(IntPtr handle);

    /// <summary>Set the local file siglevel</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_local_file_siglevel(IntPtr handle, int level);

    /// <summary>Get the default remote file siglevel</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_get_remote_file_siglevel(IntPtr handle);

    /// <summary>Set the remote file siglevel</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_remote_file_siglevel(IntPtr handle, int level);

    /// <summary>Get whether to download database files without extracting</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_get_disable_dl_timeout(IntPtr handle);

    /// <summary>Set whether to download database files without extracting</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_option_set_disable_dl_timeout(IntPtr handle, int disable_dl_timeout);

    // ============================================================================
    // alpm.h - Logging functions
    // ============================================================================

    /// <summary>A printf-like function for logging</summary>
    [DllImport(LibAlpm, CallingConvention = CallingConvention.Cdecl)]
    public static extern int alpm_logaction(IntPtr handle, byte* prefix, byte* fmt, __arglist);
}

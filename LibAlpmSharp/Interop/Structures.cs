using System;
using System.Runtime.InteropServices;

namespace LibAlpmSharp.Interop;

/// <summary>
/// A doubly linked list (alpm_list_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmList
{
    /// <summary>data held by the list node</summary>
    public void* data;
    /// <summary>pointer to the previous node</summary>
    public AlpmList* prev;
    /// <summary>pointer to the next node</summary>
    public AlpmList* next;
}

/// <summary>
/// File in a package (alpm_file_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmFile
{
    /// <summary>Name of the file</summary>
    public byte* name;
    /// <summary>Size of the file</summary>
    public long size;
    /// <summary>The file's permissions</summary>
    public uint mode;
}

/// <summary>
/// Package filelist container (alpm_filelist_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmFilelist
{
    /// <summary>Amount of files in the array</summary>
    public nuint count;
    /// <summary>An array of files</summary>
    public AlpmFile* files;
}

/// <summary>
/// Local package or package file backup entry (alpm_backup_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmBackup
{
    /// <summary>Name of the file (without .pacsave extension)</summary>
    public byte* name;
    /// <summary>Hash of the filename (used internally)</summary>
    public byte* hash;
}

/// <summary>
/// Package group (alpm_group_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmGroup
{
    /// <summary>group name</summary>
    public byte* name;
    /// <summary>list of alpm_pkg_t packages</summary>
    public AlpmList* packages;
}

/// <summary>
/// The extended data type used to store non-standard package data fields (alpm_pkg_xdata_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmPkgXData
{
    public byte* name;
    public byte* value;
}

/// <summary>
/// The basic dependency type (alpm_depend_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmDepend
{
    /// <summary>Name of the provider to satisfy this dependency</summary>
    public byte* name;
    /// <summary>Version of the provider to match against (optional)</summary>
    public byte* version;
    /// <summary>A description of why this dependency is needed (optional)</summary>
    public byte* desc;
    /// <summary>A hash of name (used internally to speed up conflict checks)</summary>
    public ulong name_hash;
    /// <summary>How the version should match against the provider</summary>
    public AlpmDepMod mod;
}

/// <summary>
/// Missing dependency (alpm_depmissing_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmDepMissing
{
    /// <summary>Name of the package that has the dependency</summary>
    public byte* target;
    /// <summary>The dependency that was wanted</summary>
    public AlpmDepend* depend;
    /// <summary>If the depmissing was caused by a conflict, the name of the package that would be installed</summary>
    public byte* causingpkg;
}

/// <summary>
/// A conflict that has occurred between two packages (alpm_conflict_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmConflict
{
    /// <summary>The first package</summary>
    public IntPtr package1;
    /// <summary>The second package</summary>
    public IntPtr package2;
    /// <summary>The conflict</summary>
    public AlpmDepend* reason;
}

/// <summary>
/// File conflict (alpm_fileconflict_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmFileConflict
{
    /// <summary>The name of the package that caused the conflict</summary>
    public byte* target;
    /// <summary>The type of conflict</summary>
    public AlpmFileConflictType type;
    /// <summary>The name of the file that the package conflicts with</summary>
    public byte* file;
    /// <summary>The name of the package that also owns the file if there is one</summary>
    public byte* ctarget;
}

/// <summary>
/// A PGP key (alpm_pgpkey_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmPgpKey
{
    /// <summary>The actual key data</summary>
    public void* data;
    /// <summary>The key's fingerprint</summary>
    public byte* fingerprint;
    /// <summary>UID of the key</summary>
    public byte* uid;
    /// <summary>Name of the key's owner</summary>
    public byte* name;
    /// <summary>Email of the key's owner</summary>
    public byte* email;
    /// <summary>When the key was created</summary>
    public long created;
    /// <summary>When the key expires</summary>
    public long expires;
    /// <summary>The length of the key</summary>
    public uint length;
    /// <summary>has the key been revoked</summary>
    public uint revoked;
}

/// <summary>
/// Signature result (alpm_sigresult_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmSigResult
{
    /// <summary>The key of the signature</summary>
    public AlpmPgpKey key;
    /// <summary>The status of the signature</summary>
    public AlpmSigStatus status;
    /// <summary>The validity of the signature</summary>
    public AlpmSigValidity validity;
}

/// <summary>
/// Signature list (alpm_siglist_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmSigList
{
    /// <summary>The amount of results in the array</summary>
    public nuint count;
    /// <summary>An array of sigresults</summary>
    public AlpmSigResult* results;
}

/// <summary>
/// An event that may represent any event (alpm_event_any_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AlpmEventAny
{
    /// <summary>Type of event</summary>
    public AlpmEventType type;
}

/// <summary>
/// A package operation event (alpm_event_package_operation_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AlpmEventPackageOperation
{
    /// <summary>Type of event</summary>
    public AlpmEventType type;
    /// <summary>Type of operation</summary>
    public AlpmPackageOperation operation;
    /// <summary>Old package</summary>
    public IntPtr oldpkg;
    /// <summary>New package</summary>
    public IntPtr newpkg;
}

/// <summary>
/// An optional dependency was removed (alpm_event_optdep_removal_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmEventOptdepRemoval
{
    /// <summary>Type of event</summary>
    public AlpmEventType type;
    /// <summary>Package with the optdep</summary>
    public IntPtr pkg;
    /// <summary>Optdep being removed</summary>
    public AlpmDepend* optdep;
}

/// <summary>
/// A scriptlet was ran (alpm_event_scriptlet_info_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmEventScriptletInfo
{
    /// <summary>Type of event</summary>
    public AlpmEventType type;
    /// <summary>Line of scriptlet output</summary>
    public byte* line;
}

/// <summary>
/// A database is missing (alpm_event_database_missing_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmEventDatabaseMissing
{
    /// <summary>Type of event</summary>
    public AlpmEventType type;
    /// <summary>Name of the database</summary>
    public byte* dbname;
}

/// <summary>
/// A pacnew file was created (alpm_event_pacnew_created_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmEventPacnewCreated
{
    /// <summary>Type of event</summary>
    public AlpmEventType type;
    /// <summary>Whether the creation was result of a NoUpgrade or not</summary>
    public int from_noupgrade;
    /// <summary>Old package</summary>
    public IntPtr oldpkg;
    /// <summary>New Package</summary>
    public IntPtr newpkg;
    /// <summary>Filename of the file without the .pacnew suffix</summary>
    public byte* file;
}

/// <summary>
/// A pacsave file was created (alpm_event_pacsave_created_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmEventPacsaveCreated
{
    /// <summary>Type of event</summary>
    public AlpmEventType type;
    /// <summary>Old package</summary>
    public IntPtr oldpkg;
    /// <summary>Filename of the file without the .pacsave suffix</summary>
    public byte* file;
}

/// <summary>
/// pre/post transaction hooks are to be ran (alpm_event_hook_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AlpmEventHook
{
    /// <summary>Type of event</summary>
    public AlpmEventType type;
    /// <summary>Type of hook</summary>
    public AlpmHookWhen when;
}

/// <summary>
/// A pre/post transaction hook was ran (alpm_event_hook_run_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmEventHookRun
{
    /// <summary>Type of event</summary>
    public AlpmEventType type;
    /// <summary>Name of hook</summary>
    public byte* name;
    /// <summary>Description of hook to be outputted</summary>
    public byte* desc;
    /// <summary>position of hook being run</summary>
    public nuint position;
    /// <summary>total hooks being run</summary>
    public nuint total;
}

/// <summary>
/// Packages downloading about to start (alpm_event_pkg_retrieve_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AlpmEventPkgRetrieve
{
    /// <summary>Type of event</summary>
    public AlpmEventType type;
    /// <summary>Number of packages to download</summary>
    public nuint num;
    /// <summary>Total size of packages to download</summary>
    public long total_size;
}

/// <summary>
/// Context struct for when a download starts (alpm_download_event_init_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AlpmDownloadEventInit
{
    /// <summary>whether this file is optional and thus the errors could be ignored</summary>
    public int optional;
}

/// <summary>
/// Context struct for when a download progresses (alpm_download_event_progress_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AlpmDownloadEventProgress
{
    /// <summary>Amount of data downloaded</summary>
    public long downloaded;
    /// <summary>Total amount need to be downloaded</summary>
    public long total;
}

/// <summary>
/// Context struct for when a download retries (alpm_download_event_retry_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AlpmDownloadEventRetry
{
    /// <summary>If the download will resume or start over</summary>
    public int resume;
}

/// <summary>
/// Context struct for when a download completes (alpm_download_event_completed_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AlpmDownloadEventCompleted
{
    /// <summary>Total bytes in file</summary>
    public long total;
    /// <summary>download result code: 0 - success, 1 - up-to-date, -1 - error</summary>
    public int result;
}

/// <summary>
/// Question - any (alpm_question_any_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AlpmQuestionAny
{
    /// <summary>The type of question</summary>
    public AlpmQuestionType type;
}

/// <summary>
/// Question - install ignorepkg (alpm_question_install_ignorepkg_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AlpmQuestionInstallIgnorepkg
{
    /// <summary>Type of question</summary>
    public AlpmQuestionType type;
    /// <summary>Answer: whether or not to install pkg anyway</summary>
    public int install;
    /// <summary>The ignored package that we are deciding whether to install</summary>
    public IntPtr pkg;
}

/// <summary>
/// Question - replace package (alpm_question_replace_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct AlpmQuestionReplace
{
    /// <summary>Type of question</summary>
    public AlpmQuestionType type;
    /// <summary>Answer: whether or not to replace oldpkg with newpkg</summary>
    public int replace;
    /// <summary>Package to be replaced</summary>
    public IntPtr oldpkg;
    /// <summary>Package to replace with</summary>
    public IntPtr newpkg;
    /// <summary>DB of newpkg</summary>
    public IntPtr newdb;
}

/// <summary>
/// Question - conflict (alpm_question_conflict_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmQuestionConflict
{
    /// <summary>Type of question</summary>
    public AlpmQuestionType type;
    /// <summary>Answer: whether or not to remove conflict->package2</summary>
    public int remove;
    /// <summary>Conflict info</summary>
    public AlpmConflict* conflict;
}

/// <summary>
/// Question - corrupted package (alpm_question_corrupted_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmQuestionCorrupted
{
    /// <summary>Type of question</summary>
    public AlpmQuestionType type;
    /// <summary>Answer: whether or not to remove filepath</summary>
    public int remove;
    /// <summary>File to remove</summary>
    public byte* filepath;
    /// <summary>Error code indicating the reason for package invalidity</summary>
    public AlpmErrno reason;
}

/// <summary>
/// Question - remove packages (alpm_question_remove_pkgs_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmQuestionRemovePkgs
{
    /// <summary>Type of question</summary>
    public AlpmQuestionType type;
    /// <summary>Answer: whether or not to skip packages</summary>
    public int skip;
    /// <summary>List of alpm_pkg_t* with unresolved dependencies</summary>
    public AlpmList* packages;
}

/// <summary>
/// Question - select provider (alpm_question_select_provider_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmQuestionSelectProvider
{
    /// <summary>Type of question</summary>
    public AlpmQuestionType type;
    /// <summary>Answer: which provider to use (index from providers)</summary>
    public int use_index;
    /// <summary>List of alpm_pkg_t* as possible providers</summary>
    public AlpmList* providers;
    /// <summary>What providers provide for</summary>
    public AlpmDepend* depend;
}

/// <summary>
/// Question - import key (alpm_question_import_key_t)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct AlpmQuestionImportKey
{
    /// <summary>Type of question</summary>
    public AlpmQuestionType type;
    /// <summary>Answer: whether or not to import key</summary>
    public int import;
    /// <summary>UID of the key to import</summary>
    public byte* uid;
    /// <summary>Fingerprint the key to import</summary>
    public byte* fingerprint;
}

using System;

namespace LibAlpmSharp.Interop;

/// <summary>
/// libalpm's error type
/// </summary>
public enum AlpmErrno
{
    /// <summary>No error</summary>
    ALPM_ERR_OK = 0,
    /// <summary>Failed to allocate memory</summary>
    ALPM_ERR_MEMORY,
    /// <summary>A system error occurred</summary>
    ALPM_ERR_SYSTEM,
    /// <summary>Permission denied</summary>
    ALPM_ERR_BADPERMS,
    /// <summary>Should be a file</summary>
    ALPM_ERR_NOT_A_FILE,
    /// <summary>Should be a directory</summary>
    ALPM_ERR_NOT_A_DIR,
    /// <summary>Function was called with invalid arguments</summary>
    ALPM_ERR_WRONG_ARGS,
    /// <summary>Insufficient disk space</summary>
    ALPM_ERR_DISK_SPACE,
    /* Interface */
    /// <summary>Handle should be null</summary>
    ALPM_ERR_HANDLE_NULL,
    /// <summary>Handle should not be null</summary>
    ALPM_ERR_HANDLE_NOT_NULL,
    /// <summary>Failed to acquire lock</summary>
    ALPM_ERR_HANDLE_LOCK,
    /* Databases */
    /// <summary>Failed to open database</summary>
    ALPM_ERR_DB_OPEN,
    /// <summary>Failed to create database</summary>
    ALPM_ERR_DB_CREATE,
    /// <summary>Database should not be null</summary>
    ALPM_ERR_DB_NULL,
    /// <summary>Database should be null</summary>
    ALPM_ERR_DB_NOT_NULL,
    /// <summary>The database could not be found</summary>
    ALPM_ERR_DB_NOT_FOUND,
    /// <summary>Database is invalid</summary>
    ALPM_ERR_DB_INVALID,
    /// <summary>Database has an invalid signature</summary>
    ALPM_ERR_DB_INVALID_SIG,
    /// <summary>The localdb is in a newer/older format than libalpm expects</summary>
    ALPM_ERR_DB_VERSION,
    /// <summary>Failed to write to the database</summary>
    ALPM_ERR_DB_WRITE,
    /// <summary>Failed to remove entry from database</summary>
    ALPM_ERR_DB_REMOVE,
    /* Servers */
    /// <summary>Server URL is in an invalid format</summary>
    ALPM_ERR_SERVER_BAD_URL,
    /// <summary>The database has no configured servers</summary>
    ALPM_ERR_SERVER_NONE,
    /* Transactions */
    /// <summary>A transaction is already initialized</summary>
    ALPM_ERR_TRANS_NOT_NULL,
    /// <summary>A transaction has not been initialized</summary>
    ALPM_ERR_TRANS_NULL,
    /// <summary>Duplicate target in transaction</summary>
    ALPM_ERR_TRANS_DUP_TARGET,
    /// <summary>Duplicate filename in transaction</summary>
    ALPM_ERR_TRANS_DUP_FILENAME,
    /// <summary>A transaction has not been initialized</summary>
    ALPM_ERR_TRANS_NOT_INITIALIZED,
    /// <summary>Transaction has not been prepared</summary>
    ALPM_ERR_TRANS_NOT_PREPARED,
    /// <summary>Transaction was aborted</summary>
    ALPM_ERR_TRANS_ABORT,
    /// <summary>Failed to interrupt transaction</summary>
    ALPM_ERR_TRANS_TYPE,
    /// <summary>Tried to commit transaction without locking the database</summary>
    ALPM_ERR_TRANS_NOT_LOCKED,
    /// <summary>A hook failed to run</summary>
    ALPM_ERR_TRANS_HOOK_FAILED,
    /* Packages */
    /// <summary>Package not found</summary>
    ALPM_ERR_PKG_NOT_FOUND,
    /// <summary>Package is in ignorepkg</summary>
    ALPM_ERR_PKG_IGNORED,
    /// <summary>Package is invalid</summary>
    ALPM_ERR_PKG_INVALID,
    /// <summary>Package has an invalid checksum</summary>
    ALPM_ERR_PKG_INVALID_CHECKSUM,
    /// <summary>Package has an invalid signature</summary>
    ALPM_ERR_PKG_INVALID_SIG,
    /// <summary>Package does not have a signature</summary>
    ALPM_ERR_PKG_MISSING_SIG,
    /// <summary>Cannot open the package file</summary>
    ALPM_ERR_PKG_OPEN,
    /// <summary>Failed to remove package files</summary>
    ALPM_ERR_PKG_CANT_REMOVE,
    /// <summary>Package has an invalid name</summary>
    ALPM_ERR_PKG_INVALID_NAME,
    /// <summary>Package has an invalid architecture</summary>
    ALPM_ERR_PKG_INVALID_ARCH,
    /* Signatures */
    /// <summary>Signatures are missing</summary>
    ALPM_ERR_SIG_MISSING,
    /// <summary>Signatures are invalid</summary>
    ALPM_ERR_SIG_INVALID,
    /* Dependencies */
    /// <summary>Dependencies could not be satisfied</summary>
    ALPM_ERR_UNSATISFIED_DEPS,
    /// <summary>Conflicting dependencies</summary>
    ALPM_ERR_CONFLICTING_DEPS,
    /// <summary>Files conflict</summary>
    ALPM_ERR_FILE_CONFLICTS,
    /* Misc */
    /// <summary>Download setup failed</summary>
    ALPM_ERR_RETRIEVE_PREPARE,
    /// <summary>Download failed</summary>
    ALPM_ERR_RETRIEVE,
    /// <summary>Invalid Regex</summary>
    ALPM_ERR_INVALID_REGEX,
    /* External library errors */
    /// <summary>Error in libarchive</summary>
    ALPM_ERR_LIBARCHIVE,
    /// <summary>Error in libcurl</summary>
    ALPM_ERR_LIBCURL,
    /// <summary>Error in external download program</summary>
    ALPM_ERR_EXTERNAL_DOWNLOAD,
    /// <summary>Error in gpgme</summary>
    ALPM_ERR_GPGME,
    /// <summary>Missing compile-time features</summary>
    ALPM_ERR_MISSING_CAPABILITY_SIGNATURES
}

/// <summary>
/// PGP signature verification options
/// </summary>
[Flags]
public enum AlpmSigLevel
{
    /// <summary>Packages require a signature</summary>
    ALPM_SIG_PACKAGE = (1 << 0),
    /// <summary>Packages do not require a signature, but check packages that do have signatures</summary>
    ALPM_SIG_PACKAGE_OPTIONAL = (1 << 1),
    /// <summary>Allow packages with signatures that are marginal trust</summary>
    ALPM_SIG_PACKAGE_MARGINAL_OK = (1 << 2),
    /// <summary>Allow packages with signatures that are unknown trust</summary>
    ALPM_SIG_PACKAGE_UNKNOWN_OK = (1 << 3),
    /// <summary>Databases require a signature</summary>
    ALPM_SIG_DATABASE = (1 << 10),
    /// <summary>Databases do not require a signature, but check databases that do have signatures</summary>
    ALPM_SIG_DATABASE_OPTIONAL = (1 << 11),
    /// <summary>Allow databases with signatures that are marginal trust</summary>
    ALPM_SIG_DATABASE_MARGINAL_OK = (1 << 12),
    /// <summary>Allow databases with signatures that are unknown trust</summary>
    ALPM_SIG_DATABASE_UNKNOWN_OK = (1 << 13),
    /// <summary>The Default siglevel</summary>
    ALPM_SIG_USE_DEFAULT = (1 << 30)
}

/// <summary>
/// PGP signature verification status return codes
/// </summary>
public enum AlpmSigStatus
{
    /// <summary>Signature is valid</summary>
    ALPM_SIGSTATUS_VALID,
    /// <summary>The key has expired</summary>
    ALPM_SIGSTATUS_KEY_EXPIRED,
    /// <summary>The signature has expired</summary>
    ALPM_SIGSTATUS_SIG_EXPIRED,
    /// <summary>The key is not in the keyring</summary>
    ALPM_SIGSTATUS_KEY_UNKNOWN,
    /// <summary>The key has been disabled</summary>
    ALPM_SIGSTATUS_KEY_DISABLED,
    /// <summary>The signature is invalid</summary>
    ALPM_SIGSTATUS_INVALID
}

/// <summary>
/// The trust level of a PGP key
/// </summary>
public enum AlpmSigValidity
{
    /// <summary>The signature is fully trusted</summary>
    ALPM_SIGVALIDITY_FULL,
    /// <summary>The signature is marginally trusted</summary>
    ALPM_SIGVALIDITY_MARGINAL,
    /// <summary>The signature is never trusted</summary>
    ALPM_SIGVALIDITY_NEVER,
    /// <summary>The signature has unknown trust</summary>
    ALPM_SIGVALIDITY_UNKNOWN
}

/// <summary>
/// Types of version constraints in dependency specs
/// </summary>
public enum AlpmDepMod
{
    /// <summary>No version constraint</summary>
    ALPM_DEP_MOD_ANY = 1,
    /// <summary>Test version equality (package=x.y.z)</summary>
    ALPM_DEP_MOD_EQ,
    /// <summary>Test for at least a version (package>=x.y.z)</summary>
    ALPM_DEP_MOD_GE,
    /// <summary>Test for at most a version (package<=x.y.z)</summary>
    ALPM_DEP_MOD_LE,
    /// <summary>Test for greater than some version (package>x.y.z)</summary>
    ALPM_DEP_MOD_GT,
    /// <summary>Test for less than some version (package&lt;x.y.z)</summary>
    ALPM_DEP_MOD_LT
}

/// <summary>
/// File conflict type
/// </summary>
public enum AlpmFileConflictType
{
    /// <summary>The conflict results with a another target in the transaction</summary>
    ALPM_FILECONFLICT_TARGET = 1,
    /// <summary>The conflict results from a file existing on the filesystem</summary>
    ALPM_FILECONFLICT_FILESYSTEM
}

/// <summary>
/// Type of events
/// </summary>
public enum AlpmEventType
{
    /// <summary>Dependencies will be computed for a package</summary>
    ALPM_EVENT_CHECKDEPS_START = 1,
    /// <summary>Dependencies were computed for a package</summary>
    ALPM_EVENT_CHECKDEPS_DONE,
    /// <summary>File conflicts will be computed for a package</summary>
    ALPM_EVENT_FILECONFLICTS_START,
    /// <summary>File conflicts were computed for a package</summary>
    ALPM_EVENT_FILECONFLICTS_DONE,
    /// <summary>Dependencies will be resolved for target package</summary>
    ALPM_EVENT_RESOLVEDEPS_START,
    /// <summary>Dependencies were resolved for target package</summary>
    ALPM_EVENT_RESOLVEDEPS_DONE,
    /// <summary>Inter-conflicts will be checked for target package</summary>
    ALPM_EVENT_INTERCONFLICTS_START,
    /// <summary>Inter-conflicts were checked for target package</summary>
    ALPM_EVENT_INTERCONFLICTS_DONE,
    /// <summary>Processing the package transaction is starting</summary>
    ALPM_EVENT_TRANSACTION_START,
    /// <summary>Processing the package transaction is finished</summary>
    ALPM_EVENT_TRANSACTION_DONE,
    /// <summary>Package will be installed/upgraded/downgraded/re-installed/removed</summary>
    ALPM_EVENT_PACKAGE_OPERATION_START,
    /// <summary>Package was installed/upgraded/downgraded/re-installed/removed</summary>
    ALPM_EVENT_PACKAGE_OPERATION_DONE,
    /// <summary>Target package's integrity will be checked</summary>
    ALPM_EVENT_INTEGRITY_START,
    /// <summary>Target package's integrity was checked</summary>
    ALPM_EVENT_INTEGRITY_DONE,
    /// <summary>Target package will be loaded</summary>
    ALPM_EVENT_LOAD_START,
    /// <summary>Target package is finished loading</summary>
    ALPM_EVENT_LOAD_DONE,
    /// <summary>Scriptlet has printed information</summary>
    ALPM_EVENT_SCRIPTLET_INFO,
    /// <summary>Database files will be downloaded from a repository</summary>
    ALPM_EVENT_DB_RETRIEVE_START,
    /// <summary>Database files were downloaded from a repository</summary>
    ALPM_EVENT_DB_RETRIEVE_DONE,
    /// <summary>Not all database files were successfully downloaded from a repository</summary>
    ALPM_EVENT_DB_RETRIEVE_FAILED,
    /// <summary>Package files will be downloaded from a repository</summary>
    ALPM_EVENT_PKG_RETRIEVE_START,
    /// <summary>Package files were downloaded from a repository</summary>
    ALPM_EVENT_PKG_RETRIEVE_DONE,
    /// <summary>Not all package files were successfully downloaded from a repository</summary>
    ALPM_EVENT_PKG_RETRIEVE_FAILED,
    /// <summary>Disk space usage will be computed for a package</summary>
    ALPM_EVENT_DISKSPACE_START,
    /// <summary>Disk space usage was computed for a package</summary>
    ALPM_EVENT_DISKSPACE_DONE,
    /// <summary>An optdepend for another package is being removed</summary>
    ALPM_EVENT_OPTDEP_REMOVAL,
    /// <summary>A configured repository database is missing</summary>
    ALPM_EVENT_DATABASE_MISSING,
    /// <summary>Checking keys used to create signatures are in keyring</summary>
    ALPM_EVENT_KEYRING_START,
    /// <summary>Keyring checking is finished</summary>
    ALPM_EVENT_KEYRING_DONE,
    /// <summary>Downloading missing keys into keyring</summary>
    ALPM_EVENT_KEY_DOWNLOAD_START,
    /// <summary>Key downloading is finished</summary>
    ALPM_EVENT_KEY_DOWNLOAD_DONE,
    /// <summary>A .pacnew file was created</summary>
    ALPM_EVENT_PACNEW_CREATED,
    /// <summary>A .pacsave file was created</summary>
    ALPM_EVENT_PACSAVE_CREATED,
    /// <summary>Processing hooks will be started</summary>
    ALPM_EVENT_HOOK_START,
    /// <summary>Processing hooks is finished</summary>
    ALPM_EVENT_HOOK_DONE,
    /// <summary>A hook is starting</summary>
    ALPM_EVENT_HOOK_RUN_START,
    /// <summary>A hook has finished running</summary>
    ALPM_EVENT_HOOK_RUN_DONE
}

/// <summary>
/// An enum over the kind of package operations
/// </summary>
public enum AlpmPackageOperation
{
    /// <summary>Package (to be) installed</summary>
    ALPM_PACKAGE_INSTALL = 1,
    /// <summary>Package (to be) upgraded</summary>
    ALPM_PACKAGE_UPGRADE,
    /// <summary>Package (to be) re-installed</summary>
    ALPM_PACKAGE_REINSTALL,
    /// <summary>Package (to be) downgraded</summary>
    ALPM_PACKAGE_DOWNGRADE,
    /// <summary>Package (to be) removed</summary>
    ALPM_PACKAGE_REMOVE
}

/// <summary>
/// An enum over different kinds of progress alerts
/// </summary>
public enum AlpmProgress
{
    /// <summary>Package install</summary>
    ALPM_PROGRESS_ADD_START,
    /// <summary>Package upgrade</summary>
    ALPM_PROGRESS_UPGRADE_START,
    /// <summary>Package downgrade</summary>
    ALPM_PROGRESS_DOWNGRADE_START,
    /// <summary>Package reinstall</summary>
    ALPM_PROGRESS_REINSTALL_START,
    /// <summary>Package removal</summary>
    ALPM_PROGRESS_REMOVE_START,
    /// <summary>Conflict checking</summary>
    ALPM_PROGRESS_CONFLICTS_START,
    /// <summary>Diskspace checking</summary>
    ALPM_PROGRESS_DISKSPACE_START,
    /// <summary>Package Integrity checking</summary>
    ALPM_PROGRESS_INTEGRITY_START,
    /// <summary>Loading packages from disk</summary>
    ALPM_PROGRESS_LOAD_START,
    /// <summary>Checking signatures of packages</summary>
    ALPM_PROGRESS_KEYRING_START
}

/// <summary>
/// File download events
/// </summary>
public enum AlpmDownloadEventType
{
    /// <summary>A download was started</summary>
    ALPM_DOWNLOAD_INIT,
    /// <summary>A download made progress</summary>
    ALPM_DOWNLOAD_PROGRESS,
    /// <summary>Download will be retried</summary>
    ALPM_DOWNLOAD_RETRY,
    /// <summary>A download completed</summary>
    ALPM_DOWNLOAD_COMPLETED
}

/// <summary>
/// The usage level of a database
/// </summary>
[Flags]
public enum AlpmDbUsage
{
    /// <summary>Enable refreshes for this database</summary>
    ALPM_DB_USAGE_SYNC = 1,
    /// <summary>Enable search for this database</summary>
    ALPM_DB_USAGE_SEARCH = (1 << 1),
    /// <summary>Enable installing packages from this database</summary>
    ALPM_DB_USAGE_INSTALL = (1 << 2),
    /// <summary>Enable sysupgrades with this database</summary>
    ALPM_DB_USAGE_UPGRADE = (1 << 3),
    /// <summary>Enable all usage levels</summary>
    ALPM_DB_USAGE_ALL = (1 << 4) - 1,
}

/// <summary>
/// Logging Levels
/// </summary>
[Flags]
public enum AlpmLogLevel
{
    /// <summary>Error</summary>
    ALPM_LOG_ERROR = 1,
    /// <summary>Warning</summary>
    ALPM_LOG_WARNING = (1 << 1),
    /// <summary>Debug</summary>
    ALPM_LOG_DEBUG = (1 << 2),
    /// <summary>Function</summary>
    ALPM_LOG_FUNCTION = (1 << 3)
}

/// <summary>
/// Transaction flags
/// </summary>
[Flags]
public enum AlpmTransFlag
{
    /// <summary>Ignore dependency checks</summary>
    ALPM_TRANS_FLAG_NODEPS = 1,
    /// <summary>Delete files even if they are tagged as backup</summary>
    ALPM_TRANS_FLAG_NOSAVE = (1 << 2),
    /// <summary>Ignore version numbers when checking dependencies</summary>
    ALPM_TRANS_FLAG_NODEPVERSION = (1 << 3),
    /// <summary>Remove also any packages depending on a package being removed</summary>
    ALPM_TRANS_FLAG_CASCADE = (1 << 4),
    /// <summary>Remove packages and their unneeded deps (not explicitly installed)</summary>
    ALPM_TRANS_FLAG_RECURSE = (1 << 5),
    /// <summary>Modify database but do not commit changes to the filesystem</summary>
    ALPM_TRANS_FLAG_DBONLY = (1 << 6),
    /// <summary>Do not run hooks during a transaction</summary>
    ALPM_TRANS_FLAG_NOHOOKS = (1 << 7),
    /// <summary>Use ALPM_PKG_REASON_DEPEND when installing packages</summary>
    ALPM_TRANS_FLAG_ALLDEPS = (1 << 8),
    /// <summary>Only download packages and do not actually install</summary>
    ALPM_TRANS_FLAG_DOWNLOADONLY = (1 << 9),
    /// <summary>Do not execute install scriptlets after installing</summary>
    ALPM_TRANS_FLAG_NOSCRIPTLET = (1 << 10),
    /// <summary>Ignore dependency conflicts</summary>
    ALPM_TRANS_FLAG_NOCONFLICTS = (1 << 11),
    /// <summary>Do not install a package if it is already installed and up to date</summary>
    ALPM_TRANS_FLAG_NEEDED = (1 << 13),
    /// <summary>Use ALPM_PKG_REASON_EXPLICIT when installing packages</summary>
    ALPM_TRANS_FLAG_ALLEXPLICIT = (1 << 14),
    /// <summary>Do not remove a package if it is needed by another one</summary>
    ALPM_TRANS_FLAG_UNNEEDED = (1 << 15),
    /// <summary>Remove also explicitly installed unneeded deps (use with ALPM_TRANS_FLAG_RECURSE)</summary>
    ALPM_TRANS_FLAG_RECURSEALL = (1 << 16),
    /// <summary>Do not lock the database during the operation</summary>
    ALPM_TRANS_FLAG_NOLOCK = (1 << 17)
}

/// <summary>
/// Package install reason
/// </summary>
public enum AlpmPkgReason
{
    /// <summary>Explicitly requested by the user</summary>
    ALPM_PKG_REASON_EXPLICIT = 0,
    /// <summary>Installed as a dependency for another package</summary>
    ALPM_PKG_REASON_DEPEND = 1
}

/// <summary>
/// Package origin
/// </summary>
public enum AlpmPkgFrom
{
    /// <summary>Package from a file</summary>
    ALPM_PKG_FROM_FILE = 1,
    /// <summary>Package from the local database</summary>
    ALPM_PKG_FROM_LOCALDB,
    /// <summary>Package from a sync database</summary>
    ALPM_PKG_FROM_SYNCDB
}

/// <summary>
/// Package validation method
/// </summary>
[Flags]
public enum AlpmPkgValidation
{
    /// <summary>Package validation is unknown</summary>
    ALPM_PKG_VALIDATION_UNKNOWN = 0,
    /// <summary>Package validation is none</summary>
    ALPM_PKG_VALIDATION_NONE = (1 << 0),
    /// <summary>Package validation is MD5 sum</summary>
    ALPM_PKG_VALIDATION_MD5SUM = (1 << 1),
    /// <summary>Package validation is SHA256 sum</summary>
    ALPM_PKG_VALIDATION_SHA256SUM = (1 << 2),
    /// <summary>Package validation is signature</summary>
    ALPM_PKG_VALIDATION_SIGNATURE = (1 << 3)
}

/// <summary>
/// Question types
/// </summary>
public enum AlpmQuestionType
{
    /// <summary>Install a package in IgnorePkg</summary>
    ALPM_QUESTION_INSTALL_IGNOREPKG = 1,
    /// <summary>Replace a package</summary>
    ALPM_QUESTION_REPLACE_PKG,
    /// <summary>Remove a conflicting package</summary>
    ALPM_QUESTION_CONFLICT_PKG,
    /// <summary>Remove a corrupted package</summary>
    ALPM_QUESTION_CORRUPTED_PKG,
    /// <summary>Remove unresolvable packages from the transaction</summary>
    ALPM_QUESTION_REMOVE_PKGS,
    /// <summary>Select a provider</summary>
    ALPM_QUESTION_SELECT_PROVIDER,
    /// <summary>Import a key</summary>
    ALPM_QUESTION_IMPORT_KEY
}

/// <summary>
/// Kind of hook
/// </summary>
public enum AlpmHookWhen
{
    /// <summary>Pre transaction hook</summary>
    ALPM_HOOK_PRE_TRANSACTION = 1,
    /// <summary>Post transaction hook</summary>
    ALPM_HOOK_POST_TRANSACTION
}

/// <summary>
/// Compile time features
/// </summary>
[Flags]
public enum AlpmCaps
{
    /// <summary>localization</summary>
    ALPM_CAPABILITY_NLS = (1 << 0),
    /// <summary>Ability to download</summary>
    ALPM_CAPABILITY_DOWNLOADER = (1 << 1),
    /// <summary>Signature checking</summary>
    ALPM_CAPABILITY_SIGNATURES = (1 << 2)
}

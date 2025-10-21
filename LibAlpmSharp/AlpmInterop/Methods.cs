using System;
using System.Runtime.InteropServices;

namespace LibAlpmSharp.AlpmInterop
{
    public static unsafe partial class Methods
    {
        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_file_t *")]
        public static extern _alpm_file_t* alpm_filelist_contains([NativeTypeName("const alpm_filelist_t *")] _alpm_filelist_t* filelist, [NativeTypeName("const char *")] byte* path);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_find_group_pkgs([NativeTypeName("alpm_list_t *")] _alpm_list_t* dbs, [NativeTypeName("const char *")] byte* name);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_errno_t")]
        public static extern _alpm_errno_t alpm_errno([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_strerror([NativeTypeName("alpm_errno_t")] _alpm_errno_t err);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_handle_t *")]
        public static extern _alpm_handle_t* alpm_initialize([NativeTypeName("const char *")] byte* root, [NativeTypeName("const char *")] byte* dbpath, [NativeTypeName("alpm_errno_t *")] _alpm_errno_t* err);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_release([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_pkg_check_pgp_signature([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg, [NativeTypeName("alpm_siglist_t *")] _alpm_siglist_t* siglist);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_db_check_pgp_signature([NativeTypeName("alpm_db_t *")] _alpm_db_t* db, [NativeTypeName("alpm_siglist_t *")] _alpm_siglist_t* siglist);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_siglist_cleanup([NativeTypeName("alpm_siglist_t *")] _alpm_siglist_t* siglist);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_decode_signature([NativeTypeName("const char *")] byte* base64_data, [NativeTypeName("unsigned char **")] byte** data, [NativeTypeName("size_t *")] nuint* data_len);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_extract_keyid([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* identifier, [NativeTypeName("const unsigned char *")] byte* sig, [NativeTypeName("const size_t")] nuint len, [NativeTypeName("alpm_list_t **")] _alpm_list_t** keys);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_checkdeps([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_list_t *")] _alpm_list_t* pkglist, [NativeTypeName("alpm_list_t *")] _alpm_list_t* remove, [NativeTypeName("alpm_list_t *")] _alpm_list_t* upgrade, int reversedeps);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_pkg_t *")]
        public static extern _alpm_pkg_t* alpm_find_satisfier([NativeTypeName("alpm_list_t *")] _alpm_list_t* pkgs, [NativeTypeName("const char *")] byte* depstring);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_pkg_t *")]
        public static extern _alpm_pkg_t* alpm_find_dbs_satisfier([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_list_t *")] _alpm_list_t* dbs, [NativeTypeName("const char *")] byte* depstring);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_checkconflicts([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_list_t *")] _alpm_list_t* pkglist);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* alpm_dep_compute_string([NativeTypeName("const alpm_depend_t *")] _alpm_depend_t* dep);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_depend_t *")]
        public static extern _alpm_depend_t* alpm_dep_from_string([NativeTypeName("const char *")] byte* depstring);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void alpm_dep_free([NativeTypeName("alpm_depend_t *")] _alpm_depend_t* dep);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void alpm_fileconflict_free([NativeTypeName("alpm_fileconflict_t *")] _alpm_fileconflict_t* conflict);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void alpm_depmissing_free([NativeTypeName("alpm_depmissing_t *")] _alpm_depmissing_t* miss);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void alpm_conflict_free([NativeTypeName("alpm_conflict_t *")] _alpm_conflict_t* conflict);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_db_t *")]
        public static extern _alpm_db_t* alpm_get_localdb([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_get_syncdbs([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_db_t *")]
        public static extern _alpm_db_t* alpm_register_syncdb([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* treename, int level);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_unregister_all_syncdbs([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_db_unregister([NativeTypeName("alpm_db_t *")] _alpm_db_t* db);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_handle_t *")]
        public static extern _alpm_handle_t* alpm_db_get_handle([NativeTypeName("alpm_db_t *")] _alpm_db_t* db);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_db_get_name([NativeTypeName("const alpm_db_t *")] _alpm_db_t* db);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_db_get_siglevel([NativeTypeName("alpm_db_t *")] _alpm_db_t* db);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_db_get_valid([NativeTypeName("alpm_db_t *")] _alpm_db_t* db);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_db_get_servers([NativeTypeName("const alpm_db_t *")] _alpm_db_t* db);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_db_set_servers([NativeTypeName("alpm_db_t *")] _alpm_db_t* db, [NativeTypeName("alpm_list_t *")] _alpm_list_t* servers);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_db_add_server([NativeTypeName("alpm_db_t *")] _alpm_db_t* db, [NativeTypeName("const char *")] byte* url);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_db_remove_server([NativeTypeName("alpm_db_t *")] _alpm_db_t* db, [NativeTypeName("const char *")] byte* url);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_db_get_cache_servers([NativeTypeName("const alpm_db_t *")] _alpm_db_t* db);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_db_set_cache_servers([NativeTypeName("alpm_db_t *")] _alpm_db_t* db, [NativeTypeName("alpm_list_t *")] _alpm_list_t* servers);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_db_add_cache_server([NativeTypeName("alpm_db_t *")] _alpm_db_t* db, [NativeTypeName("const char *")] byte* url);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_db_remove_cache_server([NativeTypeName("alpm_db_t *")] _alpm_db_t* db, [NativeTypeName("const char *")] byte* url);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_db_update([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_list_t *")] _alpm_list_t* dbs, int force);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_pkg_t *")]
        public static extern _alpm_pkg_t* alpm_db_get_pkg([NativeTypeName("alpm_db_t *")] _alpm_db_t* db, [NativeTypeName("const char *")] byte* name);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_db_get_pkgcache([NativeTypeName("alpm_db_t *")] _alpm_db_t* db);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_group_t *")]
        public static extern _alpm_group_t* alpm_db_get_group([NativeTypeName("alpm_db_t *")] _alpm_db_t* db, [NativeTypeName("const char *")] byte* name);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_db_get_groupcache([NativeTypeName("alpm_db_t *")] _alpm_db_t* db);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_db_search([NativeTypeName("alpm_db_t *")] _alpm_db_t* db, [NativeTypeName("const alpm_list_t *")] _alpm_list_t* needles, [NativeTypeName("alpm_list_t **")] _alpm_list_t** ret);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_db_set_usage([NativeTypeName("alpm_db_t *")] _alpm_db_t* db, int usage);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_db_get_usage([NativeTypeName("alpm_db_t *")] _alpm_db_t* db, int* usage);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_logaction([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* prefix, [NativeTypeName("const char *")] byte* fmt, __arglist);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_cb_log")]
        public static extern IntPtr alpm_option_get_logcb([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void* alpm_option_get_logcb_ctx([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_logcb([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_cb_log")] IntPtr cb, void* ctx);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_cb_download")]
        public static extern IntPtr alpm_option_get_dlcb([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void* alpm_option_get_dlcb_ctx([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_dlcb([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_cb_download")] IntPtr cb, void* ctx);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_cb_fetch")]
        public static extern IntPtr alpm_option_get_fetchcb([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void* alpm_option_get_fetchcb_ctx([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_fetchcb([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_cb_fetch")] IntPtr cb, void* ctx);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_cb_event")]
        public static extern IntPtr alpm_option_get_eventcb([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void* alpm_option_get_eventcb_ctx([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_eventcb([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_cb_event")] IntPtr cb, void* ctx);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_cb_question")]
        public static extern IntPtr alpm_option_get_questioncb([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void* alpm_option_get_questioncb_ctx([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_questioncb([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_cb_question")] IntPtr cb, void* ctx);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_cb_progress")]
        public static extern IntPtr alpm_option_get_progresscb([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void* alpm_option_get_progresscb_ctx([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_progresscb([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_cb_progress")] IntPtr cb, void* ctx);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_option_get_root([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_option_get_dbpath([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_option_get_lockfile([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_option_get_cachedirs([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_cachedirs([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_list_t *")] _alpm_list_t* cachedirs);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_add_cachedir([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* cachedir);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_remove_cachedir([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* cachedir);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_option_get_hookdirs([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_hookdirs([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_list_t *")] _alpm_list_t* hookdirs);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_add_hookdir([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* hookdir);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_remove_hookdir([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* hookdir);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_option_get_overwrite_files([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_overwrite_files([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_list_t *")] _alpm_list_t* globs);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_add_overwrite_file([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* glob);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_remove_overwrite_file([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* glob);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_option_get_logfile([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_logfile([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* logfile);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_option_get_gpgdir([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_gpgdir([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* gpgdir);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_option_get_sandboxuser([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_sandboxuser([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* sandboxuser);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_get_usesyslog([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_usesyslog([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, int usesyslog);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_option_get_noupgrades([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_add_noupgrade([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* path);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_noupgrades([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_list_t *")] _alpm_list_t* noupgrade);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_remove_noupgrade([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* path);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_match_noupgrade([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* path);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_option_get_noextracts([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_add_noextract([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* path);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_noextracts([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_list_t *")] _alpm_list_t* noextract);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_remove_noextract([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* path);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_match_noextract([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* path);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_option_get_ignorepkgs([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_add_ignorepkg([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_ignorepkgs([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_list_t *")] _alpm_list_t* ignorepkgs);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_remove_ignorepkg([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_option_get_ignoregroups([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_add_ignoregroup([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* grp);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_ignoregroups([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_list_t *")] _alpm_list_t* ignoregrps);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_remove_ignoregroup([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* grp);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_option_get_assumeinstalled([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_add_assumeinstalled([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const alpm_depend_t *")] _alpm_depend_t* dep);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_assumeinstalled([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_list_t *")] _alpm_list_t* deps);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_remove_assumeinstalled([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const alpm_depend_t *")] _alpm_depend_t* dep);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_option_get_architectures([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_add_architecture([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* arch);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_architectures([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_list_t *")] _alpm_list_t* arches);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_remove_architecture([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* arch);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_get_checkspace([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_checkspace([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, int checkspace);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_option_get_dbext([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_dbext([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* dbext);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_get_default_siglevel([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_default_siglevel([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, int level);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_get_local_file_siglevel([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_local_file_siglevel([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, int level);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_get_remote_file_siglevel([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_remote_file_siglevel([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, int level);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_disable_dl_timeout([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("unsigned short")] ushort disable_dl_timeout);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_get_parallel_downloads([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_parallel_downloads([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("unsigned int")] uint num_streams);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_option_set_disable_sandbox([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("unsigned short")] ushort disable_sandbox);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_pkg_load([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* filename, int full, int level, [NativeTypeName("alpm_pkg_t **")] _alpm_pkg_t** pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_fetch_pkgurl([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const alpm_list_t *")] _alpm_list_t* urls, [NativeTypeName("alpm_list_t **")] _alpm_list_t** fetched);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_pkg_t *")]
        public static extern _alpm_pkg_t* alpm_pkg_find([NativeTypeName("alpm_list_t *")] _alpm_list_t* haystack, [NativeTypeName("const char *")] byte* needle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_pkg_free([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_pkg_checkmd5sum([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_pkg_vercmp([NativeTypeName("const char *")] byte* a, [NativeTypeName("const char *")] byte* b);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_pkg_compute_requiredby([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_pkg_compute_optionalfor([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_pkg_should_ignore([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_handle_t *")]
        public static extern _alpm_handle_t* alpm_pkg_get_handle([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_pkg_get_filename([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_pkg_get_base([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_pkg_get_name([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_pkg_get_version([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_pkgfrom_t")]
        public static extern _alpm_pkgfrom_t alpm_pkg_get_origin([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_pkg_get_desc([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_pkg_get_url([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_time_t")]
        public static extern nint alpm_pkg_get_builddate([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_time_t")]
        public static extern nint alpm_pkg_get_installdate([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_pkg_get_packager([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_pkg_get_md5sum([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_pkg_get_sha256sum([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_pkg_get_arch([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("off_t")]
        public static extern nint alpm_pkg_get_size([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("off_t")]
        public static extern nint alpm_pkg_get_isize([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_pkgreason_t")]
        public static extern _alpm_pkgreason_t alpm_pkg_get_reason([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_pkg_get_licenses([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_pkg_get_groups([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_pkg_get_depends([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_pkg_get_optdepends([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_pkg_get_checkdepends([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_pkg_get_makedepends([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_pkg_get_conflicts([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_pkg_get_provides([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_pkg_get_replaces([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_filelist_t *")]
        public static extern _alpm_filelist_t* alpm_pkg_get_files([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_pkg_get_backup([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_db_t *")]
        public static extern _alpm_db_t* alpm_pkg_get_db([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_pkg_get_base64_sig([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_pkg_get_sig([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg, [NativeTypeName("unsigned char **")] byte** sig, [NativeTypeName("size_t *")] nuint* sig_len);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_pkg_get_validation([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_pkg_get_xdata([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_pkg_has_scriptlet([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("off_t")]
        public static extern nint alpm_pkg_download_size([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* newpkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_pkg_set_reason([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg, [NativeTypeName("alpm_pkgreason_t")] _alpm_pkgreason_t reason);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void* alpm_pkg_changelog_open([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern nuint alpm_pkg_changelog_read(void* ptr, [NativeTypeName("size_t")] nuint size, [NativeTypeName("const alpm_pkg_t *")] _alpm_pkg_t* pkg, void* fp);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_pkg_changelog_close([NativeTypeName("const alpm_pkg_t *")] _alpm_pkg_t* pkg, void* fp);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct archive *")]
        public static extern archive* alpm_pkg_mtree_open([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_pkg_mtree_next([NativeTypeName("const alpm_pkg_t *")] _alpm_pkg_t* pkg, [NativeTypeName("struct archive *")] archive* archive, [NativeTypeName("struct archive_entry **")] archive_entry** entry);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_pkg_mtree_close([NativeTypeName("const alpm_pkg_t *")] _alpm_pkg_t* pkg, [NativeTypeName("struct archive *")] archive* archive);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_trans_get_flags([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_trans_get_add([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_trans_get_remove([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_trans_init([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, int flags);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_trans_prepare([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_list_t **")] _alpm_list_t** data);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_trans_commit([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_list_t **")] _alpm_list_t** data);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_trans_interrupt([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_trans_release([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_sync_sysupgrade([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, int enable_downgrade);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_add_pkg([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_remove_pkg([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_pkg_t *")]
        public static extern _alpm_pkg_t* alpm_sync_get_new_version([NativeTypeName("alpm_pkg_t *")] _alpm_pkg_t* pkg, [NativeTypeName("alpm_list_t *")] _alpm_list_t* dbs_sync);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* alpm_compute_md5sum([NativeTypeName("const char *")] byte* filename);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* alpm_compute_sha256sum([NativeTypeName("const char *")] byte* filename);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_unlock([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* alpm_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_capabilities();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_sandbox_setup_child([NativeTypeName("alpm_handle_t *")] _alpm_handle_t* handle, [NativeTypeName("const char *")] byte* sandboxuser, [NativeTypeName("const char *")] byte* sandbox_path);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void alpm_list_free([NativeTypeName("alpm_list_t *")] _alpm_list_t* list);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void alpm_list_free_inner([NativeTypeName("alpm_list_t *")] _alpm_list_t* list, [NativeTypeName("alpm_list_fn_free")] IntPtr fn);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_add([NativeTypeName("alpm_list_t *")] _alpm_list_t* list, void* data);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_append([NativeTypeName("alpm_list_t **")] _alpm_list_t** list, void* data);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_append_strdup([NativeTypeName("alpm_list_t **")] _alpm_list_t** list, [NativeTypeName("const char *")] byte* data);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_add_sorted([NativeTypeName("alpm_list_t *")] _alpm_list_t* list, void* data, [NativeTypeName("alpm_list_fn_cmp")] IntPtr fn);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_join([NativeTypeName("alpm_list_t *")] _alpm_list_t* first, [NativeTypeName("alpm_list_t *")] _alpm_list_t* second);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_mmerge([NativeTypeName("alpm_list_t *")] _alpm_list_t* left, [NativeTypeName("alpm_list_t *")] _alpm_list_t* right, [NativeTypeName("alpm_list_fn_cmp")] IntPtr fn);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_msort([NativeTypeName("alpm_list_t *")] _alpm_list_t* list, [NativeTypeName("size_t")] nuint n, [NativeTypeName("alpm_list_fn_cmp")] IntPtr fn);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_remove_item([NativeTypeName("alpm_list_t *")] _alpm_list_t* haystack, [NativeTypeName("alpm_list_t *")] _alpm_list_t* item);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_remove([NativeTypeName("alpm_list_t *")] _alpm_list_t* haystack, [NativeTypeName("const void *")] void* needle, [NativeTypeName("alpm_list_fn_cmp")] IntPtr fn, void** data);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_remove_str([NativeTypeName("alpm_list_t *")] _alpm_list_t* haystack, [NativeTypeName("const char *")] byte* needle, [NativeTypeName("char **")] byte** data);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_remove_dupes([NativeTypeName("const alpm_list_t *")] _alpm_list_t* list);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_strdup([NativeTypeName("const alpm_list_t *")] _alpm_list_t* list);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_copy([NativeTypeName("const alpm_list_t *")] _alpm_list_t* list);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_copy_data([NativeTypeName("const alpm_list_t *")] _alpm_list_t* list, [NativeTypeName("size_t")] nuint size);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_reverse([NativeTypeName("alpm_list_t *")] _alpm_list_t* list);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_nth([NativeTypeName("const alpm_list_t *")] _alpm_list_t* list, [NativeTypeName("size_t")] nuint n);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_next([NativeTypeName("const alpm_list_t *")] _alpm_list_t* list);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_previous([NativeTypeName("const alpm_list_t *")] _alpm_list_t* list);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_last([NativeTypeName("const alpm_list_t *")] _alpm_list_t* list);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern nuint alpm_list_count([NativeTypeName("const alpm_list_t *")] _alpm_list_t* list);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void* alpm_list_find([NativeTypeName("const alpm_list_t *")] _alpm_list_t* haystack, [NativeTypeName("const void *")] void* needle, [NativeTypeName("alpm_list_fn_cmp")] IntPtr fn);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void* alpm_list_find_ptr([NativeTypeName("const alpm_list_t *")] _alpm_list_t* haystack, [NativeTypeName("const void *")] void* needle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* alpm_list_find_str([NativeTypeName("const alpm_list_t *")] _alpm_list_t* haystack, [NativeTypeName("const char *")] byte* needle);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int alpm_list_cmp_unsorted([NativeTypeName("const alpm_list_t *")] _alpm_list_t* left, [NativeTypeName("const alpm_list_t *")] _alpm_list_t* right, [NativeTypeName("alpm_list_fn_cmp")] IntPtr fn);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void alpm_list_diff_sorted([NativeTypeName("const alpm_list_t *")] _alpm_list_t* left, [NativeTypeName("const alpm_list_t *")] _alpm_list_t* right, [NativeTypeName("alpm_list_fn_cmp")] IntPtr fn, [NativeTypeName("alpm_list_t **")] _alpm_list_t** onlyleft, [NativeTypeName("alpm_list_t **")] _alpm_list_t** onlyright);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("alpm_list_t *")]
        public static extern _alpm_list_t* alpm_list_diff([NativeTypeName("const alpm_list_t *")] _alpm_list_t* lhs, [NativeTypeName("const alpm_list_t *")] _alpm_list_t* rhs, [NativeTypeName("alpm_list_fn_cmp")] IntPtr fn);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void* alpm_list_to_array([NativeTypeName("const alpm_list_t *")] _alpm_list_t* list, [NativeTypeName("size_t")] nuint n, [NativeTypeName("size_t")] nuint size);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_version_number();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_version_string();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_version_details();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_zlib_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_liblzma_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_bzlib_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_liblz4_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_libzstd_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_liblzo2_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_libexpat_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_libbsdxml_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_libxml2_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_mbedtls_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_nettle_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_openssl_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_libmd_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_commoncrypto_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_cng_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_wincrypt_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_librichacl_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_libacl_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_libattr_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_libiconv_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_libpcre_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_libpcre2_version();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct archive *")]
        public static extern archive* archive_read_new();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_read_support_compression_all([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_read_support_compression_bzip2([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_read_support_compression_compress([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_read_support_compression_gzip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_read_support_compression_lzip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_read_support_compression_lzma([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_read_support_compression_none([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_read_support_compression_program([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* command);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_read_support_compression_program_signature([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* param1, [NativeTypeName("const void *")] void* param2, [NativeTypeName("size_t")] nuint param3);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_read_support_compression_rpm([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_read_support_compression_uu([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_read_support_compression_xz([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_all([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_by_code([NativeTypeName("struct archive *")] archive* param0, int param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_bzip2([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_compress([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_gzip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_grzip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_lrzip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_lz4([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_lzip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_lzma([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_lzop([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_none([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_program([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* command);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_program_signature([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* param1, [NativeTypeName("const void *")] void* param2, [NativeTypeName("size_t")] nuint param3);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_rpm([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_uu([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_xz([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_filter_zstd([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_7zip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_all([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_ar([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_by_code([NativeTypeName("struct archive *")] archive* param0, int param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_cab([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_cpio([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_empty([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_gnutar([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_iso9660([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_lha([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_mtree([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_rar([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_rar5([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_raw([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_tar([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_warc([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_xar([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_zip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_zip_streamable([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_support_format_zip_seekable([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_set_format([NativeTypeName("struct archive *")] archive* param0, int param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_append_filter([NativeTypeName("struct archive *")] archive* param0, int param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_append_filter_program([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_append_filter_program_signature([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* param1, [NativeTypeName("const void *")] void* param2, [NativeTypeName("size_t")] nuint param3);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_set_open_callback([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("archive_open_callback *")] IntPtr param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_set_read_callback([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("archive_read_callback *")] IntPtr param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_set_seek_callback([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("archive_seek_callback *")] IntPtr param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_set_skip_callback([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("archive_skip_callback *")] IntPtr param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_set_close_callback([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("archive_close_callback *")] IntPtr param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_set_switch_callback([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("archive_switch_callback *")] IntPtr param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_set_callback_data([NativeTypeName("struct archive *")] archive* param0, void* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_set_callback_data2([NativeTypeName("struct archive *")] archive* param0, void* param1, [NativeTypeName("unsigned int")] uint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_add_callback_data([NativeTypeName("struct archive *")] archive* param0, void* param1, [NativeTypeName("unsigned int")] uint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_append_callback_data([NativeTypeName("struct archive *")] archive* param0, void* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_prepend_callback_data([NativeTypeName("struct archive *")] archive* param0, void* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_open1([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_open([NativeTypeName("struct archive *")] archive* param0, void* _client_data, [NativeTypeName("archive_open_callback *")] IntPtr param2, [NativeTypeName("archive_read_callback *")] IntPtr param3, [NativeTypeName("archive_close_callback *")] IntPtr param4);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_open2([NativeTypeName("struct archive *")] archive* param0, void* _client_data, [NativeTypeName("archive_open_callback *")] IntPtr param2, [NativeTypeName("archive_read_callback *")] IntPtr param3, [NativeTypeName("archive_skip_callback *")] IntPtr param4, [NativeTypeName("archive_close_callback *")] IntPtr param5);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_open_filename([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* _filename, [NativeTypeName("size_t")] nuint _block_size);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_open_filenames([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char **")] byte** _filenames, [NativeTypeName("size_t")] nuint _block_size);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_open_filename_w([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const wchar_t *")] int* _filename, [NativeTypeName("size_t")] nuint _block_size);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_read_open_file([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* _filename, [NativeTypeName("size_t")] nuint _block_size);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_open_memory([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const void *")] void* buff, [NativeTypeName("size_t")] nuint size);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_open_memory2([NativeTypeName("struct archive *")] archive* a, [NativeTypeName("const void *")] void* buff, [NativeTypeName("size_t")] nuint size, [NativeTypeName("size_t")] nuint read_size);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_open_fd([NativeTypeName("struct archive *")] archive* param0, int _fd, [NativeTypeName("size_t")] nuint _block_size);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_open_FILE([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("FILE *")] System.IntPtr _file);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_next_header([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("struct archive_entry **")] archive_entry** param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_next_header2([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("struct archive_entry *")] archive_entry* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("la_int64_t")]
        public static extern nint archive_read_header_position([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_has_encrypted_entries([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_format_capabilities([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("la_ssize_t")]
        public static extern nint archive_read_data([NativeTypeName("struct archive *")] archive* param0, void* param1, [NativeTypeName("size_t")] nuint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("la_int64_t")]
        public static extern nint archive_seek_data([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("la_int64_t")] nint param1, int param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_data_block([NativeTypeName("struct archive *")] archive* a, [NativeTypeName("const void **")] void** buff, [NativeTypeName("size_t *")] nuint* size, [NativeTypeName("la_int64_t *")] nint* offset);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_data_skip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_data_into_fd([NativeTypeName("struct archive *")] archive* param0, int fd);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_set_format_option([NativeTypeName("struct archive *")] archive* _a, [NativeTypeName("const char *")] byte* m, [NativeTypeName("const char *")] byte* o, [NativeTypeName("const char *")] byte* v);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_set_filter_option([NativeTypeName("struct archive *")] archive* _a, [NativeTypeName("const char *")] byte* m, [NativeTypeName("const char *")] byte* o, [NativeTypeName("const char *")] byte* v);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_set_option([NativeTypeName("struct archive *")] archive* _a, [NativeTypeName("const char *")] byte* m, [NativeTypeName("const char *")] byte* o, [NativeTypeName("const char *")] byte* v);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_set_options([NativeTypeName("struct archive *")] archive* _a, [NativeTypeName("const char *")] byte* opts);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_add_passphrase([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_set_passphrase_callback([NativeTypeName("struct archive *")] archive* param0, void* client_data, [NativeTypeName("archive_passphrase_callback *")] IntPtr param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_extract([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("struct archive_entry *")] archive_entry* param1, int flags);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_extract2([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("struct archive_entry *")] archive_entry* param1, [NativeTypeName("struct archive *")] archive* param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_read_extract_set_progress_callback([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("void (*)(void *)")] IntPtr _progress_func, void* _user_data);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_read_extract_set_skip_file([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("la_int64_t")] nint param1, [NativeTypeName("la_int64_t")] nint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_close([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_free([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_read_finish([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct archive *")]
        public static extern archive* archive_write_new();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_bytes_per_block([NativeTypeName("struct archive *")] archive* param0, int bytes_per_block);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_get_bytes_per_block([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_bytes_in_last_block([NativeTypeName("struct archive *")] archive* param0, int bytes_in_last_block);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_get_bytes_in_last_block([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_skip_file([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("la_int64_t")] nint param1, [NativeTypeName("la_int64_t")] nint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_write_set_compression_bzip2([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_write_set_compression_compress([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_write_set_compression_gzip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_write_set_compression_lzip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_write_set_compression_lzma([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_write_set_compression_none([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_write_set_compression_program([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* cmd);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_write_set_compression_xz([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter([NativeTypeName("struct archive *")] archive* param0, int filter_code);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter_by_name([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* name);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter_b64encode([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter_bzip2([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter_compress([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter_grzip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter_gzip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter_lrzip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter_lz4([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter_lzip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter_lzma([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter_lzop([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter_none([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter_program([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* cmd);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter_uuencode([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter_xz([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_add_filter_zstd([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format([NativeTypeName("struct archive *")] archive* param0, int format_code);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_by_name([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* name);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_7zip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_ar_bsd([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_ar_svr4([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_cpio([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_cpio_bin([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_cpio_newc([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_cpio_odc([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_cpio_pwb([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_gnutar([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_iso9660([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_mtree([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_mtree_classic([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_pax([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_pax_restricted([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_raw([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_shar([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_shar_dump([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_ustar([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_v7tar([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_warc([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_xar([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_zip([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_filter_by_ext([NativeTypeName("struct archive *")] archive* a, [NativeTypeName("const char *")] byte* filename);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_filter_by_ext_def([NativeTypeName("struct archive *")] archive* a, [NativeTypeName("const char *")] byte* filename, [NativeTypeName("const char *")] byte* def_ext);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_zip_set_compression_deflate([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_zip_set_compression_store([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_zip_set_compression_lzma([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_zip_set_compression_xz([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_zip_set_compression_bzip2([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_zip_set_compression_zstd([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_open([NativeTypeName("struct archive *")] archive* param0, void* param1, [NativeTypeName("archive_open_callback *")] IntPtr param2, [NativeTypeName("archive_write_callback *")] IntPtr param3, [NativeTypeName("archive_close_callback *")] IntPtr param4);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_open2([NativeTypeName("struct archive *")] archive* param0, void* param1, [NativeTypeName("archive_open_callback *")] IntPtr param2, [NativeTypeName("archive_write_callback *")] IntPtr param3, [NativeTypeName("archive_close_callback *")] IntPtr param4, [NativeTypeName("archive_free_callback *")] IntPtr param5);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_open_fd([NativeTypeName("struct archive *")] archive* param0, int _fd);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_open_filename([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* _file);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_open_filename_w([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const wchar_t *")] int* _file);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_write_open_file([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* _file);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_open_FILE([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("FILE *")] System.IntPtr param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_open_memory([NativeTypeName("struct archive *")] archive* param0, void* _buffer, [NativeTypeName("size_t")] nuint _buffSize, [NativeTypeName("size_t *")] nuint* _used);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_header([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("struct archive_entry *")] archive_entry* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("la_ssize_t")]
        public static extern nint archive_write_data([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const void *")] void* param1, [NativeTypeName("size_t")] nuint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("la_ssize_t")]
        public static extern nint archive_write_data_block([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const void *")] void* param1, [NativeTypeName("size_t")] nuint param2, [NativeTypeName("la_int64_t")] nint param3);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_finish_entry([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_close([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_fail([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_free([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_write_finish([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_format_option([NativeTypeName("struct archive *")] archive* _a, [NativeTypeName("const char *")] byte* m, [NativeTypeName("const char *")] byte* o, [NativeTypeName("const char *")] byte* v);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_filter_option([NativeTypeName("struct archive *")] archive* _a, [NativeTypeName("const char *")] byte* m, [NativeTypeName("const char *")] byte* o, [NativeTypeName("const char *")] byte* v);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_option([NativeTypeName("struct archive *")] archive* _a, [NativeTypeName("const char *")] byte* m, [NativeTypeName("const char *")] byte* o, [NativeTypeName("const char *")] byte* v);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_options([NativeTypeName("struct archive *")] archive* _a, [NativeTypeName("const char *")] byte* opts);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_passphrase([NativeTypeName("struct archive *")] archive* _a, [NativeTypeName("const char *")] byte* p);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_set_passphrase_callback([NativeTypeName("struct archive *")] archive* param0, void* client_data, [NativeTypeName("archive_passphrase_callback *")] IntPtr param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct archive *")]
        public static extern archive* archive_write_disk_new();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_disk_set_skip_file([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("la_int64_t")] nint param1, [NativeTypeName("la_int64_t")] nint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_disk_set_options([NativeTypeName("struct archive *")] archive* param0, int flags);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_disk_set_standard_lookup([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_disk_set_group_lookup([NativeTypeName("struct archive *")] archive* param0, void* param1, [NativeTypeName("la_int64_t (*)(void *, const char *, la_int64_t)")] IntPtr param2, [NativeTypeName("void (*)(void *)")] IntPtr param3);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_write_disk_set_user_lookup([NativeTypeName("struct archive *")] archive* param0, void* param1, [NativeTypeName("la_int64_t (*)(void *, const char *, la_int64_t)")] IntPtr param2, [NativeTypeName("void (*)(void *)")] IntPtr param3);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("la_int64_t")]
        public static extern nint archive_write_disk_gid([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* param1, [NativeTypeName("la_int64_t")] nint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("la_int64_t")]
        public static extern nint archive_write_disk_uid([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* param1, [NativeTypeName("la_int64_t")] nint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct archive *")]
        public static extern archive* archive_read_disk_new();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_set_symlink_logical([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_set_symlink_physical([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_set_symlink_hybrid([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_entry_from_file([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("struct archive_entry *")] archive_entry* param1, int param2, [NativeTypeName("const struct stat *")] stat* param3);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_read_disk_gname([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("la_int64_t")] nint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_read_disk_uname([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("la_int64_t")] nint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_set_standard_lookup([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_set_gname_lookup([NativeTypeName("struct archive *")] archive* param0, void* param1, [NativeTypeName("const char *(*)(void *, la_int64_t)")] IntPtr param2, [NativeTypeName("void (*)(void *)")] IntPtr param3);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_set_uname_lookup([NativeTypeName("struct archive *")] archive* param0, void* param1, [NativeTypeName("const char *(*)(void *, la_int64_t)")] IntPtr param2, [NativeTypeName("void (*)(void *)")] IntPtr param3);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_open([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_open_w([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const wchar_t *")] int* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_descend([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_can_descend([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_current_filesystem([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_current_filesystem_is_synthetic([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_current_filesystem_is_remote([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_set_atime_restored([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_set_behavior([NativeTypeName("struct archive *")] archive* param0, int flags);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_set_matching([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("struct archive *")] archive* _matching, [NativeTypeName("void (*)(struct archive *, void *, struct archive_entry *)")] IntPtr _excluded_func, void* _client_data);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_read_disk_set_metadata_filter_callback([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("int (*)(struct archive *, void *, struct archive_entry *)")] IntPtr _metadata_filter_func, void* _client_data);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_free([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_filter_count([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("la_int64_t")]
        public static extern nint archive_filter_bytes([NativeTypeName("struct archive *")] archive* param0, int param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_filter_code([NativeTypeName("struct archive *")] archive* param0, int param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_filter_name([NativeTypeName("struct archive *")] archive* param0, int param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("la_int64_t")]
        [Obsolete]
        public static extern nint archive_position_compressed([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("la_int64_t")]
        [Obsolete]
        public static extern nint archive_position_uncompressed([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        [Obsolete]
        public static extern byte* archive_compression_name([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [Obsolete]
        public static extern int archive_compression([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("time_t")]
        public static extern nint archive_parse_date([NativeTypeName("time_t")] nint now, [NativeTypeName("const char *")] byte* datestr);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_errno([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_error_string([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_format_name([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_format([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_clear_error([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_set_error([NativeTypeName("struct archive *")] archive* param0, int _err, [NativeTypeName("const char *")] byte* fmt, __arglist);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_copy_error([NativeTypeName("struct archive *")] archive* dest, [NativeTypeName("struct archive *")] archive* src);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_file_count([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct archive *")]
        public static extern archive* archive_match_new();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_free([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_excluded([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("struct archive_entry *")] archive_entry* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_path_excluded([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("struct archive_entry *")] archive_entry* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_set_inclusion_recursion([NativeTypeName("struct archive *")] archive* param0, int param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_exclude_pattern([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_exclude_pattern_w([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const wchar_t *")] int* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_exclude_pattern_from_file([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* param1, int _nullSeparator);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_exclude_pattern_from_file_w([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const wchar_t *")] int* param1, int _nullSeparator);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_include_pattern([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_include_pattern_w([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const wchar_t *")] int* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_include_pattern_from_file([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* param1, int _nullSeparator);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_include_pattern_from_file_w([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const wchar_t *")] int* param1, int _nullSeparator);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_path_unmatched_inclusions([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_path_unmatched_inclusions_next([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char **")] byte** param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_path_unmatched_inclusions_next_w([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const wchar_t **")] int** param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_time_excluded([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("struct archive_entry *")] archive_entry* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_include_time([NativeTypeName("struct archive *")] archive* param0, int _flag, [NativeTypeName("time_t")] nint _sec, [NativeTypeName("long")] nint _nsec);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_include_date([NativeTypeName("struct archive *")] archive* param0, int _flag, [NativeTypeName("const char *")] byte* _datestr);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_include_date_w([NativeTypeName("struct archive *")] archive* param0, int _flag, [NativeTypeName("const wchar_t *")] int* _datestr);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_include_file_time([NativeTypeName("struct archive *")] archive* param0, int _flag, [NativeTypeName("const char *")] byte* _pathname);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_include_file_time_w([NativeTypeName("struct archive *")] archive* param0, int _flag, [NativeTypeName("const wchar_t *")] int* _pathname);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_exclude_entry([NativeTypeName("struct archive *")] archive* param0, int _flag, [NativeTypeName("struct archive_entry *")] archive_entry* param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_owner_excluded([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("struct archive_entry *")] archive_entry* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_include_uid([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("la_int64_t")] nint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_include_gid([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("la_int64_t")] nint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_include_uname([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_include_uname_w([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const wchar_t *")] int* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_include_gname([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_match_include_gname_w([NativeTypeName("struct archive *")] archive* param0, [NativeTypeName("const wchar_t *")] int* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_utility_string_sort([NativeTypeName("char **")] byte** param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct archive_entry *")]
        public static extern archive_entry* archive_entry_clear([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct archive_entry *")]
        public static extern archive_entry* archive_entry_clone([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_free([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct archive_entry *")]
        public static extern archive_entry* archive_entry_new();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct archive_entry *")]
        public static extern archive_entry* archive_entry_new2([NativeTypeName("struct archive *")] archive* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("time_t")]
        public static extern nint archive_entry_atime([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("long")]
        public static extern nint archive_entry_atime_nsec([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_atime_is_set([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("time_t")]
        public static extern nint archive_entry_birthtime([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("long")]
        public static extern nint archive_entry_birthtime_nsec([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_birthtime_is_set([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("time_t")]
        public static extern nint archive_entry_ctime([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("long")]
        public static extern nint archive_entry_ctime_nsec([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_ctime_is_set([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("dev_t")]
        public static extern nuint archive_entry_dev([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_dev_is_set([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("dev_t")]
        public static extern nuint archive_entry_devmajor([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("dev_t")]
        public static extern nuint archive_entry_devminor([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("mode_t")]
        public static extern uint archive_entry_filetype([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_filetype_is_set([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_fflags([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("unsigned long *")] nuint* param1, [NativeTypeName("unsigned long *")] nuint* param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_entry_fflags_text([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("la_int64_t")]
        public static extern nint archive_entry_gid([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_gid_is_set([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_entry_gname([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_entry_gname_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const wchar_t *")]
        public static extern int* archive_entry_gname_w([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_link_to_hardlink([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_entry_hardlink([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_entry_hardlink_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const wchar_t *")]
        public static extern int* archive_entry_hardlink_w([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_hardlink_is_set([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("la_int64_t")]
        public static extern nint archive_entry_ino([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("la_int64_t")]
        public static extern nint archive_entry_ino64([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_ino_is_set([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("mode_t")]
        public static extern uint archive_entry_mode([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("time_t")]
        public static extern nint archive_entry_mtime([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("long")]
        public static extern nint archive_entry_mtime_nsec([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_mtime_is_set([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("unsigned int")]
        public static extern uint archive_entry_nlink([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_entry_pathname([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_entry_pathname_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const wchar_t *")]
        public static extern int* archive_entry_pathname_w([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("mode_t")]
        public static extern uint archive_entry_perm([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_perm_is_set([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_rdev_is_set([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("dev_t")]
        public static extern nuint archive_entry_rdev([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("dev_t")]
        public static extern nuint archive_entry_rdevmajor([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("dev_t")]
        public static extern nuint archive_entry_rdevminor([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_entry_sourcepath([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const wchar_t *")]
        public static extern int* archive_entry_sourcepath_w([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("la_int64_t")]
        public static extern nint archive_entry_size([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_size_is_set([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_entry_strmode([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_link_to_symlink([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_entry_symlink([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_entry_symlink_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_symlink_type([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const wchar_t *")]
        public static extern int* archive_entry_symlink_w([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("la_int64_t")]
        public static extern nint archive_entry_uid([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_uid_is_set([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_entry_uname([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_entry_uname_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const wchar_t *")]
        public static extern int* archive_entry_uname_w([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_is_data_encrypted([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_is_metadata_encrypted([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_is_encrypted([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_atime([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("time_t")] nint param1, [NativeTypeName("long")] nint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_unset_atime([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_birthtime([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("time_t")] nint param1, [NativeTypeName("long")] nint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_unset_birthtime([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_ctime([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("time_t")] nint param1, [NativeTypeName("long")] nint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_unset_ctime([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_dev([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("dev_t")] nuint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_devmajor([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("dev_t")] nuint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_devminor([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("dev_t")] nuint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_filetype([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("unsigned int")] uint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_fflags([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("unsigned long")] nuint param1, [NativeTypeName("unsigned long")] nuint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_entry_copy_fflags_text([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* archive_entry_copy_fflags_text_len([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1, [NativeTypeName("size_t")] nuint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const wchar_t *")]
        public static extern int* archive_entry_copy_fflags_text_w([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const wchar_t *")] int* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_gid([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("la_int64_t")] nint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_gname([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_gname_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_copy_gname([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_copy_gname_w([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const wchar_t *")] int* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_update_gname_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_hardlink([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_hardlink_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_copy_hardlink([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_copy_hardlink_w([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const wchar_t *")] int* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_update_hardlink_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_ino([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("la_int64_t")] nint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_ino64([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("la_int64_t")] nint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_link([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_link_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_copy_link([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_copy_link_w([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const wchar_t *")] int* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_update_link_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_mode([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("mode_t")] uint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_mtime([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("time_t")] nint param1, [NativeTypeName("long")] nint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_unset_mtime([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_nlink([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("unsigned int")] uint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_pathname([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_pathname_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_copy_pathname([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_copy_pathname_w([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const wchar_t *")] int* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_update_pathname_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_perm([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("mode_t")] uint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_rdev([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("dev_t")] nuint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_rdevmajor([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("dev_t")] nuint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_rdevminor([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("dev_t")] nuint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_size([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("la_int64_t")] nint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_unset_size([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_copy_sourcepath([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_copy_sourcepath_w([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const wchar_t *")] int* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_symlink([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_symlink_type([NativeTypeName("struct archive_entry *")] archive_entry* param0, int param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_symlink_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_copy_symlink([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_copy_symlink_w([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const wchar_t *")] int* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_update_symlink_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_uid([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("la_int64_t")] nint param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_uname([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_uname_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_copy_uname([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_copy_uname_w([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const wchar_t *")] int* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_update_uname_utf8([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_is_data_encrypted([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("char")] sbyte is_encrypted);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_set_is_metadata_encrypted([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("char")] sbyte is_encrypted);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const struct stat *")]
        public static extern stat* archive_entry_stat([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_copy_stat([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const struct stat *")] stat* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const void *")]
        public static extern void* archive_entry_mac_metadata([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("size_t *")] nuint* param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_copy_mac_metadata([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const void *")] void* param1, [NativeTypeName("size_t")] nuint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const unsigned char *")]
        public static extern byte* archive_entry_digest([NativeTypeName("struct archive_entry *")] archive_entry* param0, int param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_set_digest([NativeTypeName("struct archive_entry *")] archive_entry* param0, int param1, [NativeTypeName("const unsigned char *")] byte* param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_acl_clear([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_acl_add_entry([NativeTypeName("struct archive_entry *")] archive_entry* param0, int param1, int param2, int param3, int param4, [NativeTypeName("const char *")] byte* param5);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_acl_add_entry_w([NativeTypeName("struct archive_entry *")] archive_entry* param0, int param1, int param2, int param3, int param4, [NativeTypeName("const wchar_t *")] int* param5);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_acl_reset([NativeTypeName("struct archive_entry *")] archive_entry* param0, int param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_acl_next([NativeTypeName("struct archive_entry *")] archive_entry* param0, int param1, int* param2, int* param3, int* param4, int* param5, [NativeTypeName("const char **")] byte** param6);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("wchar_t *")]
        public static extern int* archive_entry_acl_to_text_w([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("la_ssize_t *")] nint* param1, int param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("char *")]
        public static extern byte* archive_entry_acl_to_text([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("la_ssize_t *")] nint* param1, int param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_acl_from_text_w([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const wchar_t *")] int* param1, int param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_acl_from_text([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1, int param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const wchar_t *")]
        [Obsolete]
        public static extern int* archive_entry_acl_text_w([NativeTypeName("struct archive_entry *")] archive_entry* param0, int param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        [Obsolete]
        public static extern byte* archive_entry_acl_text([NativeTypeName("struct archive_entry *")] archive_entry* param0, int param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_acl_types([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_acl_count([NativeTypeName("struct archive_entry *")] archive_entry* param0, int param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct archive_acl *")]
        public static extern archive_acl* archive_entry_acl([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_xattr_clear([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_xattr_add_entry([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char *")] byte* param1, [NativeTypeName("const void *")] void* param2, [NativeTypeName("size_t")] nuint param3);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_xattr_count([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_xattr_reset([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_xattr_next([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("const char **")] byte** param1, [NativeTypeName("const void **")] void** param2, [NativeTypeName("size_t *")] nuint* param3);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_sparse_clear([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_sparse_add_entry([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("la_int64_t")] nint param1, [NativeTypeName("la_int64_t")] nint param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_sparse_count([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_sparse_reset([NativeTypeName("struct archive_entry *")] archive_entry* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int archive_entry_sparse_next([NativeTypeName("struct archive_entry *")] archive_entry* param0, [NativeTypeName("la_int64_t *")] nint* param1, [NativeTypeName("la_int64_t *")] nint* param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct archive_entry_linkresolver *")]
        public static extern archive_entry_linkresolver* archive_entry_linkresolver_new();

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_linkresolver_set_strategy([NativeTypeName("struct archive_entry_linkresolver *")] archive_entry_linkresolver* param0, int param1);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_linkresolver_free([NativeTypeName("struct archive_entry_linkresolver *")] archive_entry_linkresolver* param0);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void archive_entry_linkify([NativeTypeName("struct archive_entry_linkresolver *")] archive_entry_linkresolver* param0, [NativeTypeName("struct archive_entry **")] archive_entry** param1, [NativeTypeName("struct archive_entry **")] archive_entry** param2);

        [DllImport("libalpm", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("struct archive_entry *")]
        public static extern archive_entry* archive_entry_partial_links([NativeTypeName("struct archive_entry_linkresolver *")] archive_entry_linkresolver* res, [NativeTypeName("unsigned int *")] uint* links);
    }
}

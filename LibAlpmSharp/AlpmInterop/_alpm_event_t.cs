using System.Runtime.InteropServices;

namespace LibAlpmSharp.AlpmInterop
{
    [StructLayout(LayoutKind.Explicit)]
    public partial struct _alpm_event_t
    {
        [FieldOffset(0)]
        [NativeTypeName("alpm_event_type_t")]
        public _alpm_event_type_t type;

        [FieldOffset(0)]
        [NativeTypeName("alpm_event_any_t")]
        public _alpm_event_any_t any;

        [FieldOffset(0)]
        [NativeTypeName("alpm_event_package_operation_t")]
        public _alpm_event_package_operation_t package_operation;

        [FieldOffset(0)]
        [NativeTypeName("alpm_event_optdep_removal_t")]
        public _alpm_event_optdep_removal_t optdep_removal;

        [FieldOffset(0)]
        [NativeTypeName("alpm_event_scriptlet_info_t")]
        public _alpm_event_scriptlet_info_t scriptlet_info;

        [FieldOffset(0)]
        [NativeTypeName("alpm_event_database_missing_t")]
        public _alpm_event_database_missing_t database_missing;

        [FieldOffset(0)]
        [NativeTypeName("alpm_event_pkgdownload_t")]
        public _alpm_event_pkgdownload_t pkgdownload;

        [FieldOffset(0)]
        [NativeTypeName("alpm_event_pacnew_created_t")]
        public _alpm_event_pacnew_created_t pacnew_created;

        [FieldOffset(0)]
        [NativeTypeName("alpm_event_pacsave_created_t")]
        public _alpm_event_pacsave_created_t pacsave_created;

        [FieldOffset(0)]
        [NativeTypeName("alpm_event_hook_t")]
        public _alpm_event_hook_t hook;

        [FieldOffset(0)]
        [NativeTypeName("alpm_event_hook_run_t")]
        public _alpm_event_hook_run_t hook_run;

        [FieldOffset(0)]
        [NativeTypeName("alpm_event_pkg_retrieve_t")]
        public _alpm_event_pkg_retrieve_t pkg_retrieve;
    }
}

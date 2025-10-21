namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_event_package_operation_t
    {
        [NativeTypeName("alpm_event_type_t")]
        public _alpm_event_type_t type;

        [NativeTypeName("alpm_package_operation_t")]
        public _alpm_package_operation_t operation;

        [NativeTypeName("alpm_pkg_t *")]
        public _alpm_pkg_t* oldpkg;

        [NativeTypeName("alpm_pkg_t *")]
        public _alpm_pkg_t* newpkg;
    }
}

namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_event_optdep_removal_t
    {
        [NativeTypeName("alpm_event_type_t")]
        public _alpm_event_type_t type;

        [NativeTypeName("alpm_pkg_t *")]
        public _alpm_pkg_t* pkg;

        [NativeTypeName("alpm_depend_t *")]
        public _alpm_depend_t* optdep;
    }
}

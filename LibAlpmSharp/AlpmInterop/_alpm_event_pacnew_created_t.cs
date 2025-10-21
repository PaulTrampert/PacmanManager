namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_event_pacnew_created_t
    {
        [NativeTypeName("alpm_event_type_t")]
        public _alpm_event_type_t type;

        public int from_noupgrade;

        [NativeTypeName("alpm_pkg_t *")]
        public _alpm_pkg_t* oldpkg;

        [NativeTypeName("alpm_pkg_t *")]
        public _alpm_pkg_t* newpkg;

        [NativeTypeName("const char *")]
        public byte* file;
    }
}

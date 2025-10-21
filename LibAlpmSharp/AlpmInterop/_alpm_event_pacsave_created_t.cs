namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_event_pacsave_created_t
    {
        [NativeTypeName("alpm_event_type_t")]
        public _alpm_event_type_t type;

        [NativeTypeName("alpm_pkg_t *")]
        public _alpm_pkg_t* oldpkg;

        [NativeTypeName("const char *")]
        public byte* file;
    }
}

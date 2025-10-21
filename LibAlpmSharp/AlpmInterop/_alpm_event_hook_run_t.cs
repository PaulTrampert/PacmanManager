namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_event_hook_run_t
    {
        [NativeTypeName("alpm_event_type_t")]
        public _alpm_event_type_t type;

        [NativeTypeName("const char *")]
        public byte* name;

        [NativeTypeName("const char *")]
        public byte* desc;

        [NativeTypeName("size_t")]
        public nuint position;

        [NativeTypeName("size_t")]
        public nuint total;
    }
}

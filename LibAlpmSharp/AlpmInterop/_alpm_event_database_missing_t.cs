namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_event_database_missing_t
    {
        [NativeTypeName("alpm_event_type_t")]
        public _alpm_event_type_t type;

        [NativeTypeName("const char *")]
        public byte* dbname;
    }
}

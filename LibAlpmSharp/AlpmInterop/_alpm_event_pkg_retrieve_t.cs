namespace LibAlpmSharp.AlpmInterop
{
    public partial struct _alpm_event_pkg_retrieve_t
    {
        [NativeTypeName("alpm_event_type_t")]
        public _alpm_event_type_t type;

        [NativeTypeName("size_t")]
        public nuint num;

        [NativeTypeName("off_t")]
        public nint total_size;
    }
}

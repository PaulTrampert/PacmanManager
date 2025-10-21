namespace LibAlpmSharp.AlpmInterop
{
    public partial struct _alpm_event_hook_t
    {
        [NativeTypeName("alpm_event_type_t")]
        public _alpm_event_type_t type;

        [NativeTypeName("alpm_hook_when_t")]
        public _alpm_hook_when_t when;
    }
}

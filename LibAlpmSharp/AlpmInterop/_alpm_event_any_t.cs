namespace LibAlpmSharp.AlpmInterop
{
    public partial struct _alpm_event_any_t
    {
        [NativeTypeName("alpm_event_type_t")]
        public _alpm_event_type_t type;
    }
}

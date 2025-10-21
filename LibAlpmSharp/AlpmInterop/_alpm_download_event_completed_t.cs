namespace LibAlpmSharp.AlpmInterop
{
    public partial struct _alpm_download_event_completed_t
    {
        [NativeTypeName("off_t")]
        public nint total;

        public int result;
    }
}

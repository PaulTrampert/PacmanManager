namespace LibAlpmSharp.AlpmInterop
{
    public partial struct _alpm_download_event_progress_t
    {
        [NativeTypeName("off_t")]
        public nint downloaded;

        [NativeTypeName("off_t")]
        public nint total;
    }
}

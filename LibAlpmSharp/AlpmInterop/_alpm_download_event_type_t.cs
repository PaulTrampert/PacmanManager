namespace LibAlpmSharp.AlpmInterop
{
    [NativeTypeName("unsigned int")]
    public enum _alpm_download_event_type_t : uint
    {
        ALPM_DOWNLOAD_INIT,
        ALPM_DOWNLOAD_PROGRESS,
        ALPM_DOWNLOAD_RETRY,
        ALPM_DOWNLOAD_COMPLETED,
    }
}

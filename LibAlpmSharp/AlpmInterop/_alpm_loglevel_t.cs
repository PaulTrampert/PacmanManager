namespace LibAlpmSharp.AlpmInterop
{
    [NativeTypeName("unsigned int")]
    public enum _alpm_loglevel_t : uint
    {
        ALPM_LOG_ERROR = 1,
        ALPM_LOG_WARNING = (1 << 1),
        ALPM_LOG_DEBUG = (1 << 2),
        ALPM_LOG_FUNCTION = (1 << 3),
    }
}

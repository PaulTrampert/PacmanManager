namespace LibAlpmSharp.AlpmInterop
{
    [NativeTypeName("unsigned int")]
    public enum _alpm_pkgreason_t : uint
    {
        ALPM_PKG_REASON_EXPLICIT = 0,
        ALPM_PKG_REASON_DEPEND = 1,
        ALPM_PKG_REASON_UNKNOWN = 2,
    }
}

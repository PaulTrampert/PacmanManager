namespace LibAlpmSharp.AlpmInterop
{
    [NativeTypeName("unsigned int")]
    public enum alpm_caps : uint
    {
        ALPM_CAPABILITY_NLS = (1 << 0),
        ALPM_CAPABILITY_DOWNLOADER = (1 << 1),
        ALPM_CAPABILITY_SIGNATURES = (1 << 2),
    }
}

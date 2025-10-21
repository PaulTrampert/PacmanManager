namespace LibAlpmSharp.AlpmInterop
{
    [NativeTypeName("unsigned int")]
    public enum _alpm_pkgvalidation_t : uint
    {
        ALPM_PKG_VALIDATION_UNKNOWN = 0,
        ALPM_PKG_VALIDATION_NONE = (1 << 0),
        ALPM_PKG_VALIDATION_MD5SUM = (1 << 1),
        ALPM_PKG_VALIDATION_SHA256SUM = (1 << 2),
        ALPM_PKG_VALIDATION_SIGNATURE = (1 << 3),
    }
}

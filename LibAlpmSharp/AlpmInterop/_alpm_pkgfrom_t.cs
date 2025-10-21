namespace LibAlpmSharp.AlpmInterop
{
    [NativeTypeName("unsigned int")]
    public enum _alpm_pkgfrom_t : uint
    {
        ALPM_PKG_FROM_FILE = 1,
        ALPM_PKG_FROM_LOCALDB,
        ALPM_PKG_FROM_SYNCDB,
    }
}

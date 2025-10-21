namespace LibAlpmSharp.AlpmInterop
{
    [NativeTypeName("unsigned int")]
    public enum _alpm_siglevel_t : uint
    {
        ALPM_SIG_PACKAGE = (1 << 0),
        ALPM_SIG_PACKAGE_OPTIONAL = (1 << 1),
        ALPM_SIG_PACKAGE_MARGINAL_OK = (1 << 2),
        ALPM_SIG_PACKAGE_UNKNOWN_OK = (1 << 3),
        ALPM_SIG_DATABASE = (1 << 10),
        ALPM_SIG_DATABASE_OPTIONAL = (1 << 11),
        ALPM_SIG_DATABASE_MARGINAL_OK = (1 << 12),
        ALPM_SIG_DATABASE_UNKNOWN_OK = (1 << 13),
        ALPM_SIG_USE_DEFAULT = (1 << 30),
    }
}

namespace LibAlpmSharp.AlpmInterop
{
    [NativeTypeName("unsigned int")]
    public enum _alpm_db_usage_t : uint
    {
        ALPM_DB_USAGE_SYNC = 1,
        ALPM_DB_USAGE_SEARCH = (1 << 1),
        ALPM_DB_USAGE_INSTALL = (1 << 2),
        ALPM_DB_USAGE_UPGRADE = (1 << 3),
        ALPM_DB_USAGE_ALL = (1 << 4) - 1,
    }
}

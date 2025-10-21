namespace LibAlpmSharp.AlpmInterop
{
    [NativeTypeName("unsigned int")]
    public enum _alpm_fileconflicttype_t : uint
    {
        ALPM_FILECONFLICT_TARGET = 1,
        ALPM_FILECONFLICT_FILESYSTEM,
    }
}

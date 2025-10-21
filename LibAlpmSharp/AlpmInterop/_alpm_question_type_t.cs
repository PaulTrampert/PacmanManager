namespace LibAlpmSharp.AlpmInterop
{
    [NativeTypeName("unsigned int")]
    public enum _alpm_question_type_t : uint
    {
        ALPM_QUESTION_INSTALL_IGNOREPKG = (1 << 0),
        ALPM_QUESTION_REPLACE_PKG = (1 << 1),
        ALPM_QUESTION_CONFLICT_PKG = (1 << 2),
        ALPM_QUESTION_CORRUPTED_PKG = (1 << 3),
        ALPM_QUESTION_REMOVE_PKGS = (1 << 4),
        ALPM_QUESTION_SELECT_PROVIDER = (1 << 5),
        ALPM_QUESTION_IMPORT_KEY = (1 << 6),
    }
}

namespace LibAlpmSharp.AlpmInterop
{
    [NativeTypeName("unsigned int")]
    public enum _alpm_depmod_t : uint
    {
        ALPM_DEP_MOD_ANY = 1,
        ALPM_DEP_MOD_EQ,
        ALPM_DEP_MOD_GE,
        ALPM_DEP_MOD_LE,
        ALPM_DEP_MOD_GT,
        ALPM_DEP_MOD_LT,
    }
}

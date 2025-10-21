namespace LibAlpmSharp.AlpmInterop
{
    [NativeTypeName("unsigned int")]
    public enum _alpm_hook_when_t : uint
    {
        ALPM_HOOK_PRE_TRANSACTION = 1,
        ALPM_HOOK_POST_TRANSACTION,
    }
}

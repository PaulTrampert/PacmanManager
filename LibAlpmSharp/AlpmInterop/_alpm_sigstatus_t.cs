namespace LibAlpmSharp.AlpmInterop
{
    [NativeTypeName("unsigned int")]
    public enum _alpm_sigstatus_t : uint
    {
        ALPM_SIGSTATUS_VALID,
        ALPM_SIGSTATUS_KEY_EXPIRED,
        ALPM_SIGSTATUS_SIG_EXPIRED,
        ALPM_SIGSTATUS_KEY_UNKNOWN,
        ALPM_SIGSTATUS_KEY_DISABLED,
        ALPM_SIGSTATUS_INVALID,
    }
}

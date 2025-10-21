namespace LibAlpmSharp.AlpmInterop
{
    [NativeTypeName("unsigned int")]
    public enum _alpm_sigvalidity_t : uint
    {
        ALPM_SIGVALIDITY_FULL,
        ALPM_SIGVALIDITY_MARGINAL,
        ALPM_SIGVALIDITY_NEVER,
        ALPM_SIGVALIDITY_UNKNOWN,
    }
}

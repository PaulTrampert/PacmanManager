namespace LibAlpmSharp.AlpmInterop
{
    public partial struct _alpm_sigresult_t
    {
        [NativeTypeName("alpm_pgpkey_t")]
        public _alpm_pgpkey_t key;

        [NativeTypeName("alpm_sigstatus_t")]
        public _alpm_sigstatus_t status;

        [NativeTypeName("alpm_sigvalidity_t")]
        public _alpm_sigvalidity_t validity;
    }
}

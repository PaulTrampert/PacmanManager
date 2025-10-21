namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_siglist_t
    {
        [NativeTypeName("size_t")]
        public nuint count;

        [NativeTypeName("alpm_sigresult_t *")]
        public _alpm_sigresult_t* results;
    }
}

namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_question_corrupted_t
    {
        [NativeTypeName("alpm_question_type_t")]
        public _alpm_question_type_t type;

        public int remove;

        [NativeTypeName("const char *")]
        public byte* filepath;

        [NativeTypeName("alpm_errno_t")]
        public _alpm_errno_t reason;
    }
}

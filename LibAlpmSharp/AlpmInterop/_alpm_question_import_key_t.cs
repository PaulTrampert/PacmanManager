namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_question_import_key_t
    {
        [NativeTypeName("alpm_question_type_t")]
        public _alpm_question_type_t type;

        public int import;

        [NativeTypeName("const char *")]
        public byte* uid;

        [NativeTypeName("const char *")]
        public byte* fingerprint;
    }
}

namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_question_remove_pkgs_t
    {
        [NativeTypeName("alpm_question_type_t")]
        public _alpm_question_type_t type;

        public int skip;

        [NativeTypeName("alpm_list_t *")]
        public _alpm_list_t* packages;
    }
}

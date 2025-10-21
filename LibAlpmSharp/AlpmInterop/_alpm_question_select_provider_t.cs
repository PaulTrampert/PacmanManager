namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_question_select_provider_t
    {
        [NativeTypeName("alpm_question_type_t")]
        public _alpm_question_type_t type;

        public int use_index;

        [NativeTypeName("alpm_list_t *")]
        public _alpm_list_t* providers;

        [NativeTypeName("alpm_depend_t *")]
        public _alpm_depend_t* depend;
    }
}

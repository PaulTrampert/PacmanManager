namespace LibAlpmSharp.AlpmInterop
{
    public partial struct _alpm_question_any_t
    {
        [NativeTypeName("alpm_question_type_t")]
        public _alpm_question_type_t type;

        public int answer;
    }
}

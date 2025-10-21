namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_question_conflict_t
    {
        [NativeTypeName("alpm_question_type_t")]
        public _alpm_question_type_t type;

        public int remove;

        [NativeTypeName("alpm_conflict_t *")]
        public _alpm_conflict_t* conflict;
    }
}

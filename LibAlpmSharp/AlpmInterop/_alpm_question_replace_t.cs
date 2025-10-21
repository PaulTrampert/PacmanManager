namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_question_replace_t
    {
        [NativeTypeName("alpm_question_type_t")]
        public _alpm_question_type_t type;

        public int replace;

        [NativeTypeName("alpm_pkg_t *")]
        public _alpm_pkg_t* oldpkg;

        [NativeTypeName("alpm_pkg_t *")]
        public _alpm_pkg_t* newpkg;

        [NativeTypeName("alpm_db_t *")]
        public _alpm_db_t* newdb;
    }
}

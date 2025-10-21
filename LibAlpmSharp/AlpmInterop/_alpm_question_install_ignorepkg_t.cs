namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_question_install_ignorepkg_t
    {
        [NativeTypeName("alpm_question_type_t")]
        public _alpm_question_type_t type;

        public int install;

        [NativeTypeName("alpm_pkg_t *")]
        public _alpm_pkg_t* pkg;
    }
}

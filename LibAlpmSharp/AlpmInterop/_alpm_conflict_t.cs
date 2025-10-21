namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_conflict_t
    {
        [NativeTypeName("alpm_pkg_t *")]
        public _alpm_pkg_t* package1;

        [NativeTypeName("alpm_pkg_t *")]
        public _alpm_pkg_t* package2;

        [NativeTypeName("alpm_depend_t *")]
        public _alpm_depend_t* reason;
    }
}

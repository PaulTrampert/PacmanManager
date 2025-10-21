namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_depmissing_t
    {
        [NativeTypeName("char *")]
        public byte* target;

        [NativeTypeName("alpm_depend_t *")]
        public _alpm_depend_t* depend;

        [NativeTypeName("char *")]
        public byte* causingpkg;
    }
}

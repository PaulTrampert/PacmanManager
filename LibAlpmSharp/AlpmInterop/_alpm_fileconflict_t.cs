namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_fileconflict_t
    {
        [NativeTypeName("char *")]
        public byte* target;

        [NativeTypeName("alpm_fileconflicttype_t")]
        public _alpm_fileconflicttype_t type;

        [NativeTypeName("char *")]
        public byte* file;

        [NativeTypeName("char *")]
        public byte* ctarget;
    }
}

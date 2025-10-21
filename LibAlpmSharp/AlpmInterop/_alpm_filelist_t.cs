namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_filelist_t
    {
        [NativeTypeName("size_t")]
        public nuint count;

        [NativeTypeName("alpm_file_t *")]
        public _alpm_file_t* files;
    }
}

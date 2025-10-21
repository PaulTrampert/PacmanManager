namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_file_t
    {
        [NativeTypeName("char *")]
        public byte* name;

        [NativeTypeName("off_t")]
        public nint size;

        [NativeTypeName("mode_t")]
        public uint mode;
    }
}

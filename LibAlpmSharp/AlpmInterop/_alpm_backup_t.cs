namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_backup_t
    {
        [NativeTypeName("char *")]
        public byte* name;

        [NativeTypeName("char *")]
        public byte* hash;
    }
}

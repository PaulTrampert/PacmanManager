namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_pkg_xdata_t
    {
        [NativeTypeName("char *")]
        public byte* name;

        [NativeTypeName("char *")]
        public byte* value;
    }
}

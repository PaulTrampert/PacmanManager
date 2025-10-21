namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_depend_t
    {
        [NativeTypeName("char *")]
        public byte* name;

        [NativeTypeName("char *")]
        public byte* version;

        [NativeTypeName("char *")]
        public byte* desc;

        [NativeTypeName("unsigned long")]
        public nuint name_hash;

        [NativeTypeName("alpm_depmod_t")]
        public _alpm_depmod_t mod;
    }
}

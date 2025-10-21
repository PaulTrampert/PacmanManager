namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_pgpkey_t
    {
        public void* data;

        [NativeTypeName("char *")]
        public byte* fingerprint;

        [NativeTypeName("char *")]
        public byte* uid;

        [NativeTypeName("char *")]
        public byte* name;

        [NativeTypeName("char *")]
        public byte* email;

        [NativeTypeName("alpm_time_t")]
        public nint created;

        [NativeTypeName("alpm_time_t")]
        public nint expires;

        [NativeTypeName("unsigned int")]
        public uint length;

        [NativeTypeName("unsigned int")]
        public uint revoked;

        [NativeTypeName("char")]
        public sbyte pubkey_algo;
    }
}

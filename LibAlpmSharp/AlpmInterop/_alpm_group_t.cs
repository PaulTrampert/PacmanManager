namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_group_t
    {
        [NativeTypeName("char *")]
        public byte* name;

        [NativeTypeName("alpm_list_t *")]
        public _alpm_list_t* packages;
    }
}

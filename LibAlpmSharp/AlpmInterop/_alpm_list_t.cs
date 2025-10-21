namespace LibAlpmSharp.AlpmInterop
{
    public unsafe partial struct _alpm_list_t
    {
        public void* data;

        [NativeTypeName("struct _alpm_list_t *")]
        public _alpm_list_t* prev;

        [NativeTypeName("struct _alpm_list_t *")]
        public _alpm_list_t* next;
    }
}

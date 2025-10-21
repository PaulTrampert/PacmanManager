using System.Runtime.InteropServices;

namespace LibAlpmSharp.AlpmInterop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate void alpm_cb_download(void* ctx, [NativeTypeName("const char *")] byte* filename, [NativeTypeName("alpm_download_event_type_t")] _alpm_download_event_type_t @event, void* data);
}

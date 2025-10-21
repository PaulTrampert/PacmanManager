using System.Runtime.InteropServices;

namespace LibAlpmSharp.AlpmInterop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate void alpm_cb_event(void* ctx, [NativeTypeName("alpm_event_t *")] _alpm_event_t* @event);
}

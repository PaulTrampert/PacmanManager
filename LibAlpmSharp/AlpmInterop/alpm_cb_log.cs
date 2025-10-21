using System.Runtime.InteropServices;

namespace LibAlpmSharp.AlpmInterop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate void alpm_cb_log(void* ctx, [NativeTypeName("alpm_loglevel_t")] _alpm_loglevel_t level, [NativeTypeName("const char *")] byte* fmt, [NativeTypeName("va_list")] object[] args);
}

using System.Runtime.InteropServices;

namespace LibAlpmSharp.AlpmInterop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate int alpm_cb_fetch(void* ctx, [NativeTypeName("const char *")] byte* url, [NativeTypeName("const char *")] byte* localpath, int force);
}

using System.Runtime.InteropServices;

namespace LibAlpmSharp.AlpmInterop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate int alpm_list_fn_cmp([NativeTypeName("const void *")] void* param0, [NativeTypeName("const void *")] void* param1);
}

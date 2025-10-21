using System.Runtime.InteropServices;

namespace LibAlpmSharp.AlpmInterop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate void alpm_cb_progress(void* ctx, [NativeTypeName("alpm_progress_t")] _alpm_progress_t progress, [NativeTypeName("const char *")] byte* pkg, int percent, [NativeTypeName("size_t")] nuint howmany, [NativeTypeName("size_t")] nuint current);
}

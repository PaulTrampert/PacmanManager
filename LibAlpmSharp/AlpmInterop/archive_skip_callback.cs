using System.Runtime.InteropServices;

namespace LibAlpmSharp.AlpmInterop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: NativeTypeName("la_int64_t")]
    public unsafe delegate nint archive_skip_callback([NativeTypeName("struct archive *")] archive* param0, void* _client_data, [NativeTypeName("la_int64_t")] nint request);
}

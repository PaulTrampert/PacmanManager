using System.Runtime.InteropServices;

namespace LibAlpmSharp.AlpmInterop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: NativeTypeName("la_ssize_t")]
    public unsafe delegate nint archive_read_callback([NativeTypeName("struct archive *")] archive* param0, void* _client_data, [NativeTypeName("const void **")] void** _buffer);
}

using System.Runtime.InteropServices;

namespace LibAlpmSharp.AlpmInterop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: NativeTypeName("la_ssize_t")]
    public unsafe delegate nint archive_write_callback([NativeTypeName("struct archive *")] archive* param0, void* _client_data, [NativeTypeName("const void *")] void* _buffer, [NativeTypeName("size_t")] nuint _length);
}

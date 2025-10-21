using System.Runtime.InteropServices;

namespace LibAlpmSharp.AlpmInterop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate int archive_close_callback([NativeTypeName("struct archive *")] archive* param0, void* _client_data);
}

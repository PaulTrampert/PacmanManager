using System.Runtime.InteropServices;

namespace LibAlpmSharp.AlpmInterop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: NativeTypeName("const char *")]
    public unsafe delegate byte* archive_passphrase_callback([NativeTypeName("struct archive *")] archive* param0, void* _client_data);
}

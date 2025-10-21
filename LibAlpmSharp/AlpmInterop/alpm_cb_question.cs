using System.Runtime.InteropServices;

namespace LibAlpmSharp.AlpmInterop
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate void alpm_cb_question(void* ctx, [NativeTypeName("alpm_question_t *")] _alpm_question_t* question);
}

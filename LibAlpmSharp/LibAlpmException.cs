using System.Runtime.InteropServices;
using LibAlpmSharp.AlpmInterop;

namespace LibAlpmSharp;

public class LibAlpmException(_alpm_errno_t errnum) : Exception(GetErrorMessage(errnum))
{
    public _alpm_errno_t ErrorCode { get; } = errnum;

    private static unsafe string GetErrorMessage(_alpm_errno_t errnum)
    {
        var errPtr = Methods.alpm_strerror(errnum);
        return Marshal.PtrToStringUTF8(new IntPtr(errPtr)) ?? "Unknown error";
    }
}
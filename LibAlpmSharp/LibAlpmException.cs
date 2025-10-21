using System.Runtime.InteropServices;
using LibAlpmSharp.Marshall;

namespace LibAlpmSharp;

public class LibAlpmException : Exception
{
    public AlpmErrno ErrorCode { get; }
    public LibAlpmException(AlpmErrno errnum) : base(Marshal.PtrToStringUTF8(LibAlpmMarshal.alpm_strerror(errnum)))
    {
        ErrorCode = errnum;
    }
}
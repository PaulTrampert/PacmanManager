using System.Runtime.InteropServices;

namespace LibAlpmSharp.Marshall;

internal static partial class LibAlpmMarshal
{
    internal const string LibAlpm = "libalpm";
    [LibraryImport(LibAlpm, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr alpm_initialize(string root, string dbpath, ref AlpmErrno errnum);
    
    [LibraryImport(LibAlpm)]
    internal static partial AlpmErrno alpm_release(IntPtr handle);
}
using System.Runtime.InteropServices;

namespace LibAlpmSharp.Marshall;

internal static partial class LibAlpmMarshal
{
    private const string LibAlpm = "libalpm";
    [LibraryImport(LibAlpm, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr alpm_initialize(string root, string dbpath, ref AlpmErrno errnum);
    
    [LibraryImport(LibAlpm)]
    internal static partial AlpmErrno alpm_release(IntPtr handle);
    
    [LibraryImport(LibAlpm)]
    internal static partial AlpmErrno alpm_errno(IntPtr handle);
    
    [LibraryImport(LibAlpm, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr alpm_strerror(AlpmErrno errnum);
}
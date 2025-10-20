using System.Runtime.InteropServices;

namespace LibAlpmSharp.Marshall;

public static partial class LibAlpmMarshal
{
    [LibraryImport("libalpm", StringMarshalling = StringMarshalling.Utf8)]
    public static extern partial IntPtr alpm_initialize(string root, string dbpath, ref AlpmErrno errnum);
}
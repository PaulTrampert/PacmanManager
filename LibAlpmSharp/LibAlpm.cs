using LibAlpmSharp.Marshall;

namespace LibAlpmSharp;

public class LibAlpm : IDisposable
{
    private IntPtr _alpm;
    
    public LibAlpm(string root, string dbpath)
    {
        AlpmErrno errnum = AlpmErrno.ALPM_ERR_OK;
        _alpm = LibAlpmMarshal.alpm_initialize(root, dbpath, ref errnum);
        if (errnum != AlpmErrno.ALPM_ERR_OK)
        {
            throw new LibAlpmException(errnum);
        }
    }

    public void Dispose()
    {
        LibAlpmMarshal.alpm_release(_alpm);
        GC.SuppressFinalize(this);
    }
}

public class LibAlpmException : Exception
{
    public LibAlpmException(AlpmErrno errnum)
    {
        throw new NotImplementedException();
    }
}
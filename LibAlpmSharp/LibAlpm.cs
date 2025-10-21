using System.Text;
using LibAlpmSharp.AlpmInterop;

namespace LibAlpmSharp;

public unsafe class LibAlpm : IDisposable
{
    private _alpm_handle_t* _alpm;
    
    public LibAlpm(string root, string dbpath)
    {
        var rootBytes = Encoding.UTF8.GetBytes(root);
        var dbpathBytes = Encoding.UTF8.GetBytes(dbpath);
        fixed (byte* rootPtr = rootBytes)
        fixed (byte* dbpathPtr = dbpathBytes)
        {
            _alpm_errno_t* errnum = stackalloc _alpm_errno_t[1];
            _alpm = Methods.alpm_initialize(rootPtr, dbpathPtr, errnum);
            if (*errnum != _alpm_errno_t.ALPM_ERR_OK)
            {
                throw new LibAlpmException(*errnum);
            }
        }
    }

    public void Dispose()
    {
        Methods.alpm_release(_alpm);
        GC.SuppressFinalize(this);
    }
}
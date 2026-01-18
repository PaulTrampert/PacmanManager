using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LibAlpmSharp.Interop;

namespace LibAlpmSharp;

/// <summary>
/// Represents a package database (local or sync).
/// </summary>
public sealed class AlpmDatabase
{
    private readonly LibAlpm _alpm;
    private readonly IntPtr _dbHandle;

    /// <summary>
    /// Gets the name of the database.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets whether this is the local database.
    /// </summary>
    public bool IsLocal { get; }

    /// <summary>
    /// Gets the native handle to the database.
    /// </summary>
    internal IntPtr Handle => _dbHandle;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlpmDatabase"/> class.
    /// </summary>
    /// <param name="alpm">The parent LibAlpm instance.</param>
    /// <param name="dbHandle">The native database handle.</param>
    /// <param name="isLocal">Whether this is the local database.</param>
    internal AlpmDatabase(LibAlpm alpm, IntPtr dbHandle, bool isLocal)
    {
        if (dbHandle == IntPtr.Zero)
            throw new ArgumentException("Database handle cannot be null", nameof(dbHandle));

        _alpm = alpm ?? throw new ArgumentNullException(nameof(alpm));
        _dbHandle = dbHandle;
        IsLocal = isLocal;

        unsafe
        {
            byte* namePtr = NativeMethods.alpm_db_get_name(_dbHandle);
            Name = Marshal.PtrToStringUTF8((IntPtr)namePtr) ?? string.Empty;
        }
    }

    /// <summary>
    /// Returns a string that represents the current database.
    /// </summary>
    /// <returns>A string that represents the current database.</returns>
    public override string ToString()
    {
        return $"{Name} ({(IsLocal ? "local" : "sync")})";
    }

    /// <summary>
    /// Gets the signature verification level for this database.
    /// </summary>
    /// <returns>The signature level flags.</returns>
    public int GetSignatureLevel()
    {
        return NativeMethods.alpm_db_get_siglevel(_dbHandle);
    }

    /// <summary>
    /// Checks the validity of this database.
    /// </summary>
    /// <returns>True if the database is valid, false otherwise.</returns>
    /// <exception cref="AlpmException">Thrown when an error occurs.</exception>
    public bool IsValid()
    {
        int result = NativeMethods.alpm_db_get_valid(_dbHandle);
        if (result == -1)
        {
            AlpmErrno err = _alpm.GetLastError();
            throw new AlpmException(err, $"Failed to check database validity: {LibAlpm.GetErrorString(err)}");
        }
        return result == 0;
    }

    /// <summary>
    /// Gets the list of servers configured for this database.
    /// </summary>
    /// <returns>A list of server URLs.</returns>
    public List<string> GetServers()
    {
        var servers = new List<string>();
        
        unsafe
        {
            AlpmList* serverList = NativeMethods.alpm_db_get_servers(_dbHandle);
            AlpmList* current = serverList;

            while (current != null)
            {
                if (current->data != null)
                {
                    string? server = Marshal.PtrToStringUTF8((IntPtr)current->data);
                    if (!string.IsNullOrEmpty(server))
                        servers.Add(server);
                }
                current = NativeMethods.alpm_list_next(current);
            }
        }

        return servers;
    }

    /// <summary>
    /// Adds a server URL to this database.
    /// </summary>
    /// <param name="url">The server URL to add.</param>
    /// <exception cref="ArgumentException">Thrown when url is null or empty.</exception>
    /// <exception cref="AlpmException">Thrown when the operation fails.</exception>
    public void AddServer(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Server URL cannot be null or empty", nameof(url));

        IntPtr urlPtr = IntPtr.Zero;
        try
        {
            urlPtr = Marshal.StringToHGlobalAnsi(url);
            
            unsafe
            {
                int result = NativeMethods.alpm_db_add_server(_dbHandle, (byte*)urlPtr.ToPointer());
                if (result != 0)
                {
                    AlpmErrno err = _alpm.GetLastError();
                    throw new AlpmException(err, $"Failed to add server: {LibAlpm.GetErrorString(err)}");
                }
            }
        }
        finally
        {
            if (urlPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(urlPtr);
        }
    }

    /// <summary>
    /// Removes a server URL from this database.
    /// </summary>
    /// <param name="url">The server URL to remove.</param>
    /// <returns>True if the server was removed, false if it was not found.</returns>
    /// <exception cref="ArgumentException">Thrown when url is null or empty.</exception>
    /// <exception cref="AlpmException">Thrown when an error occurs.</exception>
    public bool RemoveServer(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Server URL cannot be null or empty", nameof(url));

        IntPtr urlPtr = IntPtr.Zero;
        try
        {
            urlPtr = Marshal.StringToHGlobalAnsi(url);
            
            unsafe
            {
                int result = NativeMethods.alpm_db_remove_server(_dbHandle, (byte*)urlPtr.ToPointer());
                if (result == 0)
                    return true;
                if (result == 1)
                    return false;
                
                AlpmErrno err = _alpm.GetLastError();
                throw new AlpmException(err, $"Failed to remove server: {LibAlpm.GetErrorString(err)}");
            }
        }
        finally
        {
            if (urlPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(urlPtr);
        }
    }
}

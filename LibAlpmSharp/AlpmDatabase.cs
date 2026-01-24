using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LibAlpmSharp.Interop;

namespace LibAlpmSharp;

/// <summary>
/// Represents a package database (local or sync).
/// </summary>
public sealed class AlpmDatabase : IAlpmDatabase
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
    public IEnumerable<string> GetServers()
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

    /// <summary>
    /// Gets a package from this database by name.
    /// </summary>
    /// <param name="name">The name of the package to find.</param>
    /// <returns>The package if found, null otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when name is null or empty.</exception>
    public IPackage? GetPackage(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Package name cannot be null or empty", nameof(name));

        IntPtr namePtr = IntPtr.Zero;
        try
        {
            namePtr = Marshal.StringToHGlobalAnsi(name);
            
            unsafe
            {
                IntPtr pkgHandle = NativeMethods.alpm_db_get_pkg(_dbHandle, (byte*)namePtr.ToPointer());
                if (pkgHandle == IntPtr.Zero)
                    return null;

                return new AlpmPackage(pkgHandle);
            }
        }
        finally
        {
            if (namePtr != IntPtr.Zero)
                Marshal.FreeHGlobal(namePtr);
        }
    }

    /// <summary>
    /// Gets all packages in this database.
    /// </summary>
    /// <returns>A list of all packages in the database.</returns>
    public IEnumerable<IPackage> GetPackages()
    {
        var packages = new List<AlpmPackage>();
        
        unsafe
        {
            AlpmList* pkgList = NativeMethods.alpm_db_get_pkgcache(_dbHandle);
            AlpmList* current = pkgList;

            while (current != null)
            {
                IntPtr pkgHandle = (IntPtr)current->data;
                if (pkgHandle != IntPtr.Zero)
                {
                    packages.Add(new AlpmPackage(pkgHandle));
                }
                current = NativeMethods.alpm_list_next(current);
            }
        }

        return packages;
    }

    /// <summary>
    /// Searches for packages in this database matching the given terms.
    /// </summary>
    /// <param name="searchTerms">The search terms to match against package names and descriptions.</param>
    /// <returns>A list of packages matching the search terms.</returns>
    /// <exception cref="ArgumentException">Thrown when searchTerms is null or empty.</exception>
    /// <exception cref="AlpmException">Thrown when the search fails.</exception>
    public IEnumerable<IPackage> Search(params string[] searchTerms)
    {
        if (searchTerms == null || searchTerms.Length == 0)
            throw new ArgumentException("Search terms cannot be null or empty", nameof(searchTerms));

        var packages = new List<AlpmPackage>();
        
        unsafe
        {
            // Build search list
            AlpmList* needles = null;
            List<IntPtr> allocatedPtrs = new List<IntPtr>();

            try
            {
                foreach (var term in searchTerms)
                {
                    if (!string.IsNullOrWhiteSpace(term))
                    {
                        IntPtr termPtr = Marshal.StringToHGlobalAnsi(term);
                        allocatedPtrs.Add(termPtr);
                        needles = NativeMethods.alpm_list_add(needles, (void*)termPtr.ToPointer());
                    }
                }

                AlpmList* results = null;
                int ret = NativeMethods.alpm_db_search(_dbHandle, needles, &results);

                if (ret != 0)
                {
                    AlpmErrno err = _alpm.GetLastError();
                    throw new AlpmException(err, $"Failed to search database: {LibAlpm.GetErrorString(err)}");
                }

                // Extract packages from results
                AlpmList* current = results;
                while (current != null)
                {
                    IntPtr pkgHandle = (IntPtr)current->data;
                    if (pkgHandle != IntPtr.Zero)
                    {
                        packages.Add(new AlpmPackage(pkgHandle));
                    }
                    current = NativeMethods.alpm_list_next(current);
                }

                // Free the result list (not the package handles, they're owned by the database)
                if (results != null)
                    NativeMethods.alpm_list_free(results);
            }
            finally
            {
                // Free the search term list
                if (needles != null)
                    NativeMethods.alpm_list_free(needles);
                
                // Free allocated string pointers
                foreach (var ptr in allocatedPtrs)
                {
                    if (ptr != IntPtr.Zero)
                        Marshal.FreeHGlobal(ptr);
                }
            }
        }

        return packages;
    }
}

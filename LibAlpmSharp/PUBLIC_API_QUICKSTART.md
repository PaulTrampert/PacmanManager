# LibAlpmSharp Public API - Quick Start Guide

## Installation

```csharp
using LibAlpmSharp;
```

## Basic Usage

### Initialize the Library

```csharp
// With default paths (/ and /var/lib/pacman)
using (var alpm = LibAlpm.Initialize())
{
    Console.WriteLine($"Initialized at {alpm.Root}");
}

// With custom paths
using (var alpm = LibAlpm.Initialize("/", "/var/lib/pacman"))
{
    Console.WriteLine($"Database at {alpm.DbPath}");
}
```

### Get Library Information

```csharp
// Get version
string version = LibAlpm.GetVersion();
Console.WriteLine($"libalpm version: {version}");

// Get capabilities
AlpmCaps caps = LibAlpm.GetCapabilities();
if (caps.HasFlag(AlpmCaps.ALPM_CAPABILITY_SIGNATURES))
{
    Console.WriteLine("Signature checking is supported");
}
```

### Error Handling

```csharp
try
{
    using (var alpm = LibAlpm.Initialize())
    {
        // Check for errors
        AlpmErrno error = alpm.GetLastError();
        if (error != AlpmErrno.ALPM_ERR_OK)
        {
            string errorMsg = LibAlpm.GetErrorString(error);
            Console.WriteLine($"Error: {errorMsg}");
        }
    }
}
catch (AlpmException ex)
{
    Console.WriteLine($"Failed: {ex.Message}");
    Console.WriteLine($"Error code: {ex.ErrorCode}");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Invalid argument: {ex.Message}");
}
```

## Complete Example

```csharp
using System;
using LibAlpmSharp;

class Program
{
    static void Main()
    {
        // Display library information
        Console.WriteLine($"LibAlpm Version: {LibAlpm.GetVersion()}");
        
        AlpmCaps caps = LibAlpm.GetCapabilities();
        Console.WriteLine($"Capabilities: {caps}");
        
        // Initialize the library
        try
        {
            using (LibAlpm alpm = LibAlpm.Initialize())
            {
                Console.WriteLine($"Successfully initialized");
                Console.WriteLine($"  Root: {alpm.Root}");
                Console.WriteLine($"  Database: {alpm.DbPath}");
                
                // Check for any errors
                AlpmErrno error = alpm.GetLastError();
                if (error != AlpmErrno.ALPM_ERR_OK)
                {
                    Console.WriteLine($"Warning: {LibAlpm.GetErrorString(error)}");
                }
                
                // TODO: Use the library to query packages, etc.
            }
            
            Console.WriteLine("Library disposed successfully");
        }
        catch (AlpmException ex)
        {
            Console.Error.WriteLine($"Alpm Error: {ex.Message}");
            Console.Error.WriteLine($"Error Code: {ex.ErrorCode}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
```

## Error Codes

Common error codes you might encounter:

| Error Code | Description |
|------------|-------------|
| `ALPM_ERR_OK` | No error |
| `ALPM_ERR_MEMORY` | Out of memory |
| `ALPM_ERR_SYSTEM` | System error |
| `ALPM_ERR_BADPERMS` | Permission denied |
| `ALPM_ERR_HANDLE_LOCK` | Failed to acquire lock |
| `ALPM_ERR_DB_OPEN` | Failed to open database |
| `ALPM_ERR_PKG_NOT_FOUND` | Package not found |

See `AlpmErrno` enum for the complete list.

## Best Practices

### 1. Always Use `using` Statement

```csharp
// ✅ Good - automatic disposal
using (var alpm = LibAlpm.Initialize())
{
    // Use alpm
}

// ❌ Bad - manual disposal required
var alpm = LibAlpm.Initialize();
try
{
    // Use alpm
}
finally
{
    alpm.Dispose();
}
```

### 2. Handle Exceptions Appropriately

```csharp
try
{
    using (var alpm = LibAlpm.Initialize())
    {
        // Operations
    }
}
catch (AlpmException ex) when (ex.ErrorCode == AlpmErrno.ALPM_ERR_BADPERMS)
{
    Console.WriteLine("Need elevated permissions");
}
catch (AlpmException ex)
{
    Console.WriteLine($"Alpm error: {ex.Message}");
}
```

### 3. Check Errors When Needed

```csharp
using (var alpm = LibAlpm.Initialize())
{
    // After potentially error-prone operations
    AlpmErrno error = alpm.GetLastError();
    if (error != AlpmErrno.ALPM_ERR_OK)
    {
        // Handle error
    }
}
```

### 4. Use Static Methods for Library Info

```csharp
// Before initialization
Console.WriteLine($"Using libalpm {LibAlpm.GetVersion()}");

// Check capabilities before using features
if (LibAlpm.GetCapabilities().HasFlag(AlpmCaps.ALPM_CAPABILITY_DOWNLOADER))
{
    // Use download features
}
```

## Common Scenarios

### Check if Library Can Be Initialized

```csharp
bool CanInitializeLibrary()
{
    try
    {
        using (var alpm = LibAlpm.Initialize())
        {
            return true;
        }
    }
    catch (AlpmException)
    {
        return false;
    }
}
```

### Verify Library Version

```csharp
bool IsVersionSupported(string minVersion)
{
    string current = LibAlpm.GetVersion();
    // Parse and compare versions
    return true; // Simplified
}
```

### Handle Permission Errors

```csharp
try
{
    using (var alpm = LibAlpm.Initialize())
    {
        // Operations
    }
}
catch (AlpmException ex) when (ex.ErrorCode == AlpmErrno.ALPM_ERR_BADPERMS)
{
    Console.WriteLine("This operation requires elevated permissions.");
    Console.WriteLine("Try running with sudo or as root.");
}
```

## Tips

1. **Initialization is Expensive** - Initialize once and reuse the instance
2. **Thread Safety** - Each thread should have its own `LibAlpm` instance
3. **Disposal** - Always dispose properly to release file locks
4. **Error Checking** - Check `GetLastError()` after critical operations
5. **Permissions** - Some operations require root/sudo access

## What's Next?

After initializing the library, you can:

1. **Access Databases** - Query local and sync databases (coming soon)
2. **Search Packages** - Find packages by name or description (coming soon)
3. **Manage Transactions** - Install, remove, or upgrade packages (coming soon)
4. **Check Dependencies** - Resolve package dependencies (coming soon)

## API Reference

### LibAlpm Class

```csharp
public sealed class LibAlpm : IDisposable
{
    // Factory methods
    public static LibAlpm Initialize(string root, string dbPath);
    public static LibAlpm Initialize();
    
    // Static utility methods
    public static string GetVersion();
    public static AlpmCaps GetCapabilities();
    public static string GetErrorString(AlpmErrno err);
    
    // Instance methods
    public AlpmErrno GetLastError();
    public void Dispose();
    
    // Properties
    public string Root { get; }
    public string DbPath { get; }
}
```

### AlpmException Class

```csharp
public class AlpmException : Exception
{
    public AlpmErrno ErrorCode { get; }
    
    // Multiple constructors available
}
```

### AlpmErrno Enum

```csharp
public enum AlpmErrno
{
    ALPM_ERR_OK,
    ALPM_ERR_MEMORY,
    ALPM_ERR_SYSTEM,
    ALPM_ERR_BADPERMS,
    // ... and many more
}
```

### AlpmCaps Enum

```csharp
[Flags]
public enum AlpmCaps
{
    ALPM_CAPABILITY_NLS,
    ALPM_CAPABILITY_DOWNLOADER,
    ALPM_CAPABILITY_SIGNATURES
}
```

## Support

For issues or questions:
- Check error messages and error codes
- Ensure proper permissions (some operations need root)
- Verify libalpm is installed on the system
- Check that database paths are correct

# LibAlpmSharp Interop Layer

This directory contains the C# interop layer for libalpm (Arch Linux Package Manager library).

## Overview

The interop layer provides P/Invoke declarations and structures for interacting with `/lib/libalpm.so`. It includes:

- **Enums.cs**: All enumerations from libalpm headers
- **Structures.cs**: All native structures used by libalpm
- **Delegates.cs**: Callback delegate definitions
- **NativeMethods.cs**: Core P/Invoke declarations for list operations, error handling, database operations, and options
- **NativeMethods.Package.cs**: P/Invoke declarations for package operations, transactions, and miscellaneous functions

## API Coverage

The interop layer covers the following libalpm APIs:

### List Operations (alpm_list.h)
- List creation, manipulation, and searching
- List sorting and merging
- String list operations

### Core Handle Operations
- `alpm_initialize()` - Initialize the library
- `alpm_release()` - Release the library
- `alpm_errno()` - Get error codes
- `alpm_strerror()` - Get error messages

### Database Operations
- Local and sync database access
- Database registration/unregistration
- Package and group querying
- Database updates
- Server management

### Package Operations
- Package loading and freeing
- Package metadata access (name, version, description, etc.)
- Package file lists
- Package dependencies, conflicts, and provides
- Package changelog and mtree access
- Package signature verification

### Transaction Operations
- Transaction initialization and release
- Transaction preparation and commit
- Package addition and removal
- System upgrade operations

### Signature Verification
- PGP signature checking for packages and databases
- Key management
- Signature decoding

### Dependency Handling
- Dependency checking
- Conflict detection
- Satisfier resolution

### Options and Configuration
- Callback configuration (log, event, question, progress, download)
- Path configuration (root, dbpath, cachedir, hookdir, etc.)
- Package lists (ignorepkg, ignoregroup, noupgrade, noextract)
- Architecture configuration
- Signature level configuration

### Miscellaneous
- Version comparison
- Checksum computation (MD5, SHA256)
- Library version and capabilities

## Usage Example

```csharp
using LibAlpmSharp.Interop;

unsafe
{
    AlpmErrno err;
    IntPtr handle = NativeMethods.alpm_initialize(
        (byte*)Marshal.StringToHGlobalAnsi("/").ToPointer(),
        (byte*)Marshal.StringToHGlobalAnsi("/var/lib/pacman").ToPointer(),
        &err);
    
    if (handle == IntPtr.Zero)
    {
        var errStr = Marshal.PtrToStringUTF8((IntPtr)NativeMethods.alpm_strerror(err));
        Console.WriteLine($"Failed to initialize: {errStr}");
        return;
    }
    
    // Use the library...
    
    NativeMethods.alpm_release(handle);
}
```

## Notes

- All functions use `CallingConvention.Cdecl`
- The library is linked as "libalpm.so"
- String parameters are `byte*` (UTF-8 encoded)
- Unsafe code is required for most operations
- Memory management must be handled carefully to avoid leaks

## Safety Considerations

This is a low-level interop layer. Users should:

1. **Handle Memory Carefully**: Use `fixed` statements or `GCHandle` for pinning managed memory
2. **Check Return Values**: Most functions return 0 on success, -1 on error
3. **Check Errors**: Use `alpm_errno()` to get detailed error information
4. **Free Resources**: Call appropriate free functions for allocated structures
5. **Use SafeHandles**: Consider wrapping native handles in `SafeHandle` derivatives

## Building Higher-Level APIs

This interop layer is intentionally low-level. It's recommended to build higher-level, safe wrappers on top of this layer that:

- Provide automatic memory management
- Use C# strings instead of byte pointers
- Wrap native handles in `SafeHandle` or similar patterns
- Provide LINQ-style enumeration for lists
- Use events instead of callbacks
- Throw exceptions instead of returning error codes

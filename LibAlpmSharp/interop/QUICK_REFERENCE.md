# LibAlpmSharp Interop - Quick Reference

## Most Common Operations

### 1. Initialize and Release

```csharp
unsafe
{
    AlpmErrno err;
    IntPtr handle = NativeMethods.alpm_initialize(
        (byte*)Marshal.StringToHGlobalAnsi("/").ToPointer(),
        (byte*)Marshal.StringToHGlobalAnsi("/var/lib/pacman").ToPointer(),
        &err);
    
    // Use handle...
    
    NativeMethods.alpm_release(handle);
}
```

### 2. Get Library Information

```csharp
// Get version
byte* versionPtr = NativeMethods.alpm_version();
string version = Marshal.PtrToStringUTF8((IntPtr)versionPtr);

// Get capabilities
int caps = NativeMethods.alpm_capabilities();
bool hasDownloader = (caps & (int)AlpmCaps.ALPM_CAPABILITY_DOWNLOADER) != 0;
```

### 3. Access Databases

```csharp
// Get local database
IntPtr localdb = NativeMethods.alpm_get_localdb(handle);

// Register sync database
IntPtr syncdb = NativeMethods.alpm_register_syncdb(
    handle, 
    (byte*)Marshal.StringToHGlobalAnsi("core").ToPointer(),
    (int)AlpmSigLevel.ALPM_SIG_USE_DEFAULT);

// Get all sync databases
AlpmList* syncdbs = NativeMethods.alpm_get_syncdbs(handle);
```

### 4. Query Packages

```csharp
// Get package by name
IntPtr pkg = NativeMethods.alpm_db_get_pkg(
    db,
    (byte*)Marshal.StringToHGlobalAnsi("pacman").ToPointer());

// Get all packages
AlpmList* packages = NativeMethods.alpm_db_get_pkgcache(db);
nuint count = NativeMethods.alpm_list_count(packages);
```

### 5. Access Package Information

```csharp
// Package name
byte* namePtr = NativeMethods.alpm_pkg_get_name(pkg);
string name = Marshal.PtrToStringUTF8((IntPtr)namePtr);

// Package version
byte* versionPtr = NativeMethods.alpm_pkg_get_version(pkg);
string version = Marshal.PtrToStringUTF8((IntPtr)versionPtr);

// Package description
byte* descPtr = NativeMethods.alpm_pkg_get_desc(pkg);
string desc = Marshal.PtrToStringUTF8((IntPtr)descPtr);

// Package size
long size = NativeMethods.alpm_pkg_get_isize(pkg);

// Package dependencies
AlpmList* deps = NativeMethods.alpm_pkg_get_depends(pkg);
```

### 6. Iterate Over Lists

```csharp
unsafe
{
    AlpmList* list = NativeMethods.alpm_db_get_pkgcache(db);
    AlpmList* current = list;
    
    while (current != null)
    {
        IntPtr pkg = (IntPtr)current->data;
        
        // Process package...
        byte* namePtr = NativeMethods.alpm_pkg_get_name(pkg);
        string name = Marshal.PtrToStringUTF8((IntPtr)namePtr);
        
        current = NativeMethods.alpm_list_next(current);
    }
}
```

### 7. Transactions

```csharp
// Initialize transaction
int result = NativeMethods.alpm_trans_init(
    handle, 
    (int)AlpmTransFlag.ALPM_TRANS_FLAG_NODEPS);

// Add package to transaction
NativeMethods.alpm_add_pkg(handle, pkg);

// Prepare transaction
AlpmList* data = null;
result = NativeMethods.alpm_trans_prepare(handle, &data);

// Commit transaction
result = NativeMethods.alpm_trans_commit(handle, &data);

// Release transaction
NativeMethods.alpm_trans_release(handle);
```

### 8. Error Handling

```csharp
AlpmErrno err = NativeMethods.alpm_errno(handle);
if (err != AlpmErrno.ALPM_ERR_OK)
{
    byte* errPtr = NativeMethods.alpm_strerror(err);
    string errMsg = Marshal.PtrToStringUTF8((IntPtr)errPtr);
    Console.WriteLine($"Error: {errMsg}");
}
```

### 9. Compare Versions

```csharp
int cmp = NativeMethods.alpm_pkg_vercmp(
    (byte*)Marshal.StringToHGlobalAnsi("1.0.0").ToPointer(),
    (byte*)Marshal.StringToHGlobalAnsi("1.0.1").ToPointer());

// cmp < 0: first version is older
// cmp = 0: versions are equal
// cmp > 0: first version is newer
```

### 10. Search Databases

```csharp
// Create search list
AlpmList* needles = null;
needles = NativeMethods.alpm_list_add(
    needles,
    (void*)Marshal.StringToHGlobalAnsi("kernel").ToPointer());

AlpmList* results = null;
int ret = NativeMethods.alpm_db_search(db, needles, &results);

// Iterate results...
```

## Helper Functions

### Convert string to byte pointer

```csharp
IntPtr StringToPtr(string str)
{
    return Marshal.StringToHGlobalAnsi(str);
}

unsafe byte* StringToByte(string str)
{
    return (byte*)Marshal.StringToHGlobalAnsi(str).ToPointer();
}
```

### Convert byte pointer to string

```csharp
unsafe string PtrToString(byte* ptr)
{
    return Marshal.PtrToStringUTF8((IntPtr)ptr) ?? string.Empty;
}
```

### Free string pointer

```csharp
void FreePtr(IntPtr ptr)
{
    if (ptr != IntPtr.Zero)
        Marshal.FreeHGlobal(ptr);
}
```

## Common Patterns

### Safe String Handling

```csharp
IntPtr strPtr = IntPtr.Zero;
try
{
    strPtr = Marshal.StringToHGlobalAnsi("my-string");
    // Use (byte*)strPtr.ToPointer()...
}
finally
{
    if (strPtr != IntPtr.Zero)
        Marshal.FreeHGlobal(strPtr);
}
```

### Safe Handle Usage

```csharp
unsafe
{
    AlpmErrno err;
    IntPtr handle = IntPtr.Zero;
    IntPtr root = IntPtr.Zero;
    IntPtr dbpath = IntPtr.Zero;
    
    try
    {
        root = Marshal.StringToHGlobalAnsi("/");
        dbpath = Marshal.StringToHGlobalAnsi("/var/lib/pacman");
        
        handle = NativeMethods.alpm_initialize(
            (byte*)root.ToPointer(),
            (byte*)dbpath.ToPointer(),
            &err);
        
        if (handle == IntPtr.Zero)
            throw new Exception("Failed to initialize");
        
        // Use handle...
    }
    finally
    {
        if (handle != IntPtr.Zero)
            NativeMethods.alpm_release(handle);
        if (root != IntPtr.Zero)
            Marshal.FreeHGlobal(root);
        if (dbpath != IntPtr.Zero)
            Marshal.FreeHGlobal(dbpath);
    }
}
```

## Important Flags

### Transaction Flags
```csharp
AlpmTransFlag.ALPM_TRANS_FLAG_NODEPS        // Ignore dependencies
AlpmTransFlag.ALPM_TRANS_FLAG_DOWNLOADONLY  // Only download
AlpmTransFlag.ALPM_TRANS_FLAG_DBONLY        // Only update database
AlpmTransFlag.ALPM_TRANS_FLAG_RECURSE       // Remove unneeded deps
```

### Signature Levels
```csharp
AlpmSigLevel.ALPM_SIG_PACKAGE              // Require package signature
AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL     // Optional package signature
AlpmSigLevel.ALPM_SIG_DATABASE             // Require database signature
AlpmSigLevel.ALPM_SIG_USE_DEFAULT          // Use default level
```

### Database Usage
```csharp
AlpmDbUsage.ALPM_DB_USAGE_SYNC     // Enable refreshes
AlpmDbUsage.ALPM_DB_USAGE_SEARCH   // Enable search
AlpmDbUsage.ALPM_DB_USAGE_INSTALL  // Enable installs
AlpmDbUsage.ALPM_DB_USAGE_UPGRADE  // Enable upgrades
AlpmDbUsage.ALPM_DB_USAGE_ALL      // Enable all
```

## Return Values

Most functions follow these conventions:

- **0** = Success
- **-1** = Error (check `alpm_errno()`)
- **NULL/IntPtr.Zero** = Not found or error
- **Positive number** = Count or result value

Always check return values and handle errors appropriately!

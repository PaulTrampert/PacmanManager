# Package Lookup Functionality - Implementation Complete

## Summary

Successfully added package lookup functionality to LibAlpmSharp. This includes a new `AlpmPackage` class and three package query methods in `AlpmDatabase`.

## Files Created/Modified

### 1. New File: `LibAlpmSharp/AlpmPackage.cs` (160 lines)

A safe, managed wrapper for libalpm package handles (`alpm_pkg_t*`).

**Public Properties:**
- `string Name` - Package name
- `string Version` - Package version
- `string Description` - Package description
- `IntPtr Handle` (internal) - Native handle

**Public Methods:**
- `string GetBase()` - Package base name
- `string GetUrl()` - Package URL
- `string GetArchitecture()` - Build architecture
- `string GetPackager()` - Packager name
- `long GetInstalledSize()` - Installed size in bytes
- `long GetDownloadSize()` - Download size in bytes
- `long GetBuildDate()` - Build date (Unix timestamp)
- `long GetInstallDate()` - Install date (Unix timestamp)
- `string ToString()` - Returns "name version"

### 2. Modified: `LibAlpmSharp/AlpmDatabase.cs`

Added three new methods for package queries:

```csharp
// Get a specific package by name
public AlpmPackage? GetPackage(string name)

// Get all packages in the database
public List<AlpmPackage> GetPackages()

// Search for packages matching search terms
public List<AlpmPackage> Search(params string[] searchTerms)
```

### 3. Modified: `LibAlpmSharp.Test/AlpmDatabaseTests.cs`

Added 7 new tests for package lookup functionality:

1. ✅ `GetPackage_WithValidName_ReturnsPackage`
2. ✅ `GetPackage_WithInvalidName_ReturnsNull`
3. ✅ `GetPackage_WithNullName_ThrowsArgumentException`
4. ✅ `GetPackages_ReturnsListOfPackages`
5. ✅ `Search_WithValidTerm_ReturnsMatchingPackages`
6. ✅ `Search_WithNullTerms_ThrowsArgumentException`
7. ✅ `Search_WithEmptyTerms_ThrowsArgumentException`

## Test Results

✅ **All 37 tests pass** (0 failed, 0 skipped)

**Sample Test Output:**
```
Found package: 7zip 25.01-1
  Description: File archiver for extremely high compression

Found 1627 packages in local database
First package: 7zip 25.01-1
  Description: File archiver for extremely high compression
  Architecture: x86_64

Search for '7zi' found 1 packages
  - 7zip 25.01-1
```

## Usage Examples

### Get a Specific Package

```csharp
using (var alpm = LibAlpm.Initialize())
{
    var localDb = alpm.GetLocalDatabase();
    
    AlpmPackage? pkg = localDb.GetPackage("pacman");
    if (pkg != null)
    {
        Console.WriteLine($"Package: {pkg}");
        Console.WriteLine($"Description: {pkg.Description}");
        Console.WriteLine($"Architecture: {pkg.GetArchitecture()}");
        Console.WriteLine($"Installed Size: {pkg.GetInstalledSize()} bytes");
    }
}
```

### Get All Packages

```csharp
using (var alpm = LibAlpm.Initialize())
{
    var localDb = alpm.GetLocalDatabase();
    
    var packages = localDb.GetPackages();
    Console.WriteLine($"Found {packages.Count} installed packages:");
    
    foreach (var pkg in packages.Take(10))
    {
        Console.WriteLine($"  {pkg} - {pkg.Description}");
    }
}
```

### Search for Packages

```csharp
using (var alpm = LibAlpm.Initialize())
{
    var localDb = alpm.GetLocalDatabase();
    
    // Search for packages matching "python"
    var results = localDb.Search("python");
    Console.WriteLine($"Found {results.Count} packages matching 'python':");
    
    foreach (var pkg in results)
    {
        Console.WriteLine($"  {pkg}");
    }
    
    // Search with multiple terms
    var libResults = localDb.Search("lib", "network");
    Console.WriteLine($"Found {libResults.Count} packages matching lib OR network");
}
```

### Query Package Details

```csharp
using (var alpm = LibAlpm.Initialize())
{
    var localDb = alpm.GetLocalDatabase();
    var pkg = localDb.GetPackage("bash");
    
    if (pkg != null)
    {
        Console.WriteLine($"Name: {pkg.Name}");
        Console.WriteLine($"Version: {pkg.Version}");
        Console.WriteLine($"Description: {pkg.Description}");
        Console.WriteLine($"Base: {pkg.GetBase()}");
        Console.WriteLine($"URL: {pkg.GetUrl()}");
        Console.WriteLine($"Packager: {pkg.GetPackager()}");
        Console.WriteLine($"Architecture: {pkg.GetArchitecture()}");
        Console.WriteLine($"Installed Size: {pkg.GetInstalledSize()} bytes");
        Console.WriteLine($"Download Size: {pkg.GetDownloadSize()} bytes");
        
        long buildDate = pkg.GetBuildDate();
        Console.WriteLine($"Build Date: {DateTimeOffset.FromUnixTimeSeconds(buildDate)}");
        
        long installDate = pkg.GetInstallDate();
        if (installDate > 0)
        {
            Console.WriteLine($"Install Date: {DateTimeOffset.FromUnixTimeSeconds(installDate)}");
        }
    }
}
```

## Implementation Details

### AlpmPackage Class

- **Immutable**: Package properties are cached at construction
- **Lightweight**: Only stores the native handle and three cached strings
- **Safe**: No IDisposable needed - handles are owned by the database
- **Efficient**: Additional properties are retrieved on-demand via methods

### GetPackage Method

- Returns `null` if package not found
- Validates input parameters
- Uses `alpm_db_get_pkg()` for O(1) lookup by name

### GetPackages Method

- Returns all packages in the database
- Iterates the package cache list (`alpm_db_get_pkgcache`)
- Creates new `AlpmPackage` wrappers for each entry
- No filtering applied

### Search Method

- Accepts multiple search terms (params array)
- Terms are combined with OR logic (matches any term)
- Uses `alpm_db_search()` for efficient searching
- Searches both package names and descriptions
- Properly manages native memory for search list

## API Summary

### AlpmPackage

```csharp
public sealed class AlpmPackage
{
    // Properties
    public string Name { get; }
    public string Version { get; }
    public string Description { get; }
    
    // Methods
    public string GetBase();
    public string GetUrl();
    public string GetArchitecture();
    public string GetPackager();
    public long GetInstalledSize();
    public long GetDownloadSize();
    public long GetBuildDate();
    public long GetInstallDate();
    public override string ToString();
}
```

### AlpmDatabase (New Methods)

```csharp
public sealed class AlpmDatabase
{
    // ...existing members...
    
    // NEW: Package lookup methods
    public AlpmPackage? GetPackage(string name);
    public List<AlpmPackage> GetPackages();
    public List<AlpmPackage> Search(params string[] searchTerms);
}
```

## Design Decisions

### 1. Nullable Return for GetPackage

Returns `AlpmPackage?` (nullable) to indicate when a package is not found:
- ✅ Clear API - null means not found
- ✅ No exceptions for normal "not found" case
- ✅ Follows .NET conventions (Dictionary.TryGetValue pattern)

### 2. Cached Properties

Name, Version, and Description are cached at construction:
- ✅ Performance - frequently accessed properties
- ✅ Immutable - package metadata doesn't change
- ✅ Simple - reduces native interop calls

### 3. On-Demand Properties via Methods

Less common properties use methods (GetArchitecture, GetPackager, etc.):
- ✅ Clear that they involve work
- ✅ Avoid caching rarely-used data
- ✅ Balance between performance and memory

### 4. Params Array for Search

`Search(params string[] searchTerms)` allows flexible usage:
```csharp
db.Search("python");              // Single term
db.Search("python", "lib");       // Multiple terms
db.Search(new[] {"x", "y", "z"}); // Array
```

## Memory Management

- Package handles are owned by the database (no manual free needed)
- Search creates temporary native list that is freed after use
- String allocations are freed in finally blocks
- No IDisposable needed on AlpmPackage

## Performance Notes

- `GetPackage()` is O(1) - direct hash lookup
- `GetPackages()` iterates entire cache - O(n)
- `Search()` uses libalpm's optimized search - faster than manual iteration
- Package cache is maintained by libalpm (no redundant queries)

## Thread Safety

- Each `LibAlpm` instance should be used by single thread
- `AlpmPackage` objects are immutable (thread-safe to read)
- Don't share `AlpmPackage` across different `LibAlpm` instances

## Next Steps

With package lookup complete, future enhancements could include:

1. **Package Dependencies** - Query depends, optdepends, conflicts, provides
2. **Package Files** - Get list of files installed by package
3. **Package Groups** - Query package groups
4. **Version Comparison** - Compare package versions
5. **Package Transactions** - Install, remove, upgrade packages

## Conclusion

✅ **Complete!** Package lookup functionality is fully implemented with:

- Safe, managed AlpmPackage class
- Three query methods (GetPackage, GetPackages, Search)
- Comprehensive test coverage (7 new tests)
- Clean, intuitive API
- All 37 tests passing

Users can now query packages from databases, search for packages by name/description, and access detailed package metadata through a safe, managed API.

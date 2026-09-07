namespace LibAlpmSharp;

/// <summary>
/// Represents a package, either one belonging to a database or one loaded from a file.
/// </summary>
/// <remarks>
/// A package that came from a database is owned by that database and disposing it does nothing.
/// A package loaded by <see cref="ILibAlpm.LoadPackageFile"/> owns its native handle and must be
/// disposed, which is why the interface is disposable at all.
/// </remarks>
public interface IPackage : IDisposable
{
    /// <summary>
    /// Gets the package name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the package version.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Gets the package description.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Returns a string that represents the current package.
    /// </summary>
    /// <returns>A string that represents the current package.</returns>
    string ToString();

    /// <summary>
    /// Gets the package base name.
    /// </summary>
    /// <returns>The base name of the package.</returns>
    string GetBase();

    /// <summary>
    /// Gets the package URL.
    /// </summary>
    /// <returns>The URL of the package.</returns>
    string GetUrl();

    /// <summary>
    /// Gets the architecture for which the package was built.
    /// </summary>
    /// <returns>The architecture string.</returns>
    string GetArchitecture();

    /// <summary>
    /// Gets the packager's name.
    /// </summary>
    /// <returns>The packager's name.</returns>
    string GetPackager();

    /// <summary>
    /// Gets the installed size of the package.
    /// </summary>
    /// <returns>The installed size in bytes.</returns>
    long GetInstalledSize();

    /// <summary>
    /// Gets the download size of the package.
    /// </summary>
    /// <returns>The download size in bytes.</returns>
    long GetDownloadSize();

    /// <summary>
    /// Gets the build date of the package.
    /// </summary>
    /// <returns>The build date.</returns>
    DateTimeOffset GetBuildDate();

    /// <summary>
    /// Gets the install date of the package.
    /// </summary>
    /// <remarks>
    /// libalpm reports a zero timestamp for a package that is not installed — every package loaded
    /// from a file, for instance — which this returns as <see langword="null"/> rather than as the
    /// Unix epoch.
    /// </remarks>
    /// <returns>The install date, or <see langword="null"/> if the package is not installed.</returns>
    DateTimeOffset? GetInstallDate();

    /// <summary>
    /// Gets the name of the file the package was loaded from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is not a basename. For a package loaded with <see cref="ILibAlpm.LoadPackageFile"/>
    /// libalpm reports back the path it was handed, so a package read out of a temporary directory
    /// reports that temporary path.
    /// </para>
    /// <para>
    /// <b>Nothing may use this to name a stored file.</b> The packages API derives the name it
    /// stores a package under from the package's own metadata. This member is bound for
    /// completeness only.
    /// </para>
    /// </remarks>
    /// <returns>The file name, or <see langword="null"/> if the package did not come from a file.</returns>
    string? GetFileName();

    /// <summary>
    /// Gets the package's SHA256 checksum.
    /// </summary>
    /// <remarks>
    /// libalpm populates this from a sync database entry, so it is <see langword="null"/> for a
    /// package loaded from a file with <see cref="ILibAlpm.LoadPackageFile"/>. Callers that need a
    /// checksum of an uploaded file must compute it themselves over the bytes they received.
    /// </remarks>
    /// <returns>The 64 lowercase hexadecimal digit checksum, or <see langword="null"/> if libalpm has none.</returns>
    string? GetSha256Sum();

    /// <summary>
    /// Gets the package's MD5 checksum.
    /// </summary>
    /// <remarks>
    /// libalpm populates this from a sync database entry, so it is <see langword="null"/> for a
    /// package loaded from a file with <see cref="ILibAlpm.LoadPackageFile"/>. Callers that need a
    /// checksum of an uploaded file must compute it themselves over the bytes they received.
    /// </remarks>
    /// <returns>The 32 lowercase hexadecimal digit checksum, or <see langword="null"/> if libalpm has none.</returns>
    string? GetMd5Sum();

    /// <summary>
    /// Gets the licenses the package is distributed under.
    /// </summary>
    /// <returns>A list of license identifiers, empty if the package declares none.</returns>
    List<string> GetLicenses();

    /// <summary>
    /// Gets the groups the package belongs to.
    /// </summary>
    /// <returns>A list of group names, empty if the package belongs to none.</returns>
    List<string> GetGroups();

    /// <summary>
    /// Gets the list of package dependencies.
    /// </summary>
    /// <returns>A list of dependencies.</returns>
    List<AlpmDependency> GetDependencies();

    /// <summary>
    /// Gets the list of package optional dependencies.
    /// </summary>
    /// <returns>A list of optional dependencies.</returns>
    List<AlpmDependency> GetOptionalDependencies();

    /// <summary>
    /// Computes the list of packages requiring this package.
    /// Note: The returned list must be freed by the caller.
    /// </summary>
    /// <returns>A list of package names that require this package.</returns>
    List<string> GetRequiredBy();

    /// <summary>
    /// Computes the list of packages that optionally require this package.
    /// Note: The returned list must be freed by the caller.
    /// </summary>
    /// <returns>A list of package names that optionally require this package.</returns>
    List<string> GetOptionalFor();

    /// <summary>
    /// Gets the list of packages that conflict with this package.
    /// </summary>
    /// <returns>A list of conflicting package dependencies.</returns>
    List<AlpmDependency> GetConflicts();

    /// <summary>
    /// Gets the list of virtual packages this package provides.
    /// </summary>
    /// <returns>A list of provisions, each of which may carry a version.</returns>
    List<AlpmDependency> GetProvides();

    /// <summary>
    /// Gets the list of packages this package replaces.
    /// </summary>
    /// <returns>A list of replaced packages.</returns>
    List<AlpmDependency> GetReplaces();

    /// <summary>
    /// Gets the list of dependencies required to build the package.
    /// </summary>
    /// <returns>A list of make dependencies.</returns>
    List<AlpmDependency> GetMakeDepends();

    /// <summary>
    /// Gets the list of dependencies required to run the package's test suite.
    /// </summary>
    /// <returns>A list of check dependencies.</returns>
    List<AlpmDependency> GetCheckDepends();
}
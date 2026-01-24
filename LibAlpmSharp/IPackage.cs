namespace LibAlpmSharp;

public interface IPackage
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
    /// <returns>The install date, or null if not installed.</returns>
    DateTimeOffset? GetInstallDate();

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
}
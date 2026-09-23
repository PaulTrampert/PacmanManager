namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Thrown when an upload would put an <c>any</c> build of a package beside an architecture specific
/// build of the same name, in either order.
/// </summary>
/// <remarks>
/// <para>
/// An <c>any</c> package is listed in every supported architecture's database, so it and an
/// architecture specific build of the same name would be two entries for one name in the same
/// database. Arch applies the same rule. Builds for two different specific architectures never
/// share a database, and may coexist.
/// </para>
/// <para>
/// Like <see cref="PackageNotNewerException"/>, this is a statement about the repository's contents
/// rather than about the file, so it is a conflict: the same upload is accepted once the other build
/// is deleted.
/// </para>
/// </remarks>
/// <param name="packageName">The package being published.</param>
/// <param name="offeredArchitecture">The architecture the upload was built for.</param>
/// <param name="publishedArchitecture">The architecture of the build the repository already holds.</param>
public class PackageArchitectureConflictException(
    string packageName,
    string offeredArchitecture,
    string publishedArchitecture)
    : Exception(
        $"This repository already holds '{packageName}' built for '{publishedArchitecture}', which cannot "
        + $"be published alongside a build for '{offeredArchitecture}'. Delete it first.")
{
    /// <summary>
    /// The package being published.
    /// </summary>
    public string PackageName { get; } = packageName;

    /// <summary>
    /// The architecture the upload was built for.
    /// </summary>
    public string OfferedArchitecture { get; } = offeredArchitecture;

    /// <summary>
    /// The architecture of the build the repository already holds.
    /// </summary>
    public string PublishedArchitecture { get; } = publishedArchitecture;
}

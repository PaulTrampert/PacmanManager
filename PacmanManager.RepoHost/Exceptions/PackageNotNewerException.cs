namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Thrown when an upload does not move a package forward: the repository already holds that
/// package, for that architecture, at the offered version or a newer one.
/// </summary>
/// <remarks>
/// <para>
/// Pacman only ever rolls forward, so a repository holds exactly one version of a package and a
/// published version is one clients may already have downloaded, cached and recorded a checksum
/// for. Overwriting it in place would leave those clients with bytes that no longer match the
/// <c>%SHA256SUM%</c> in the database they synced, which is neither detectable nor repairable from
/// the outside — so the second push of a version is refused instead.
/// </para>
/// <para>
/// This is a statement about the repository's contents rather than about the file, which is why it
/// is a conflict and not one of the <see cref="InvalidPackageException"/> rejections: the same
/// upload would have been accepted a moment earlier, and a rebuild published under a new
/// <c>pkgrel</c> is accepted now.
/// </para>
/// </remarks>
/// <param name="packageName">The package that is already published.</param>
/// <param name="publishedVersion">The version the repository already holds.</param>
/// <param name="offeredVersion">The version that was offered.</param>
/// <param name="architecture">The architecture both were built for.</param>
public class PackageNotNewerException(
    string packageName,
    string publishedVersion,
    string offeredVersion,
    string architecture)
    : Exception(
        $"This repository already holds '{packageName}' {publishedVersion} for '{architecture}', "
        + $"which is not older than the '{offeredVersion}' offered. Publish a later version instead.")
{
    /// <summary>
    /// The package that is already published.
    /// </summary>
    public string PackageName { get; } = packageName;

    /// <summary>
    /// The version the repository already holds.
    /// </summary>
    public string PublishedVersion { get; } = publishedVersion;

    /// <summary>
    /// The version that was offered.
    /// </summary>
    public string OfferedVersion { get; } = offeredVersion;

    /// <summary>
    /// The architecture both versions were built for.
    /// </summary>
    public string Architecture { get; } = architecture;
}

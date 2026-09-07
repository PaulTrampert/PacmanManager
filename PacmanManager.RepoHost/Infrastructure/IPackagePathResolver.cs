namespace PacmanManager.RepoHost.Infrastructure;

/// <summary>
/// Owns where a repository's package files live on disk, and what a stored package file is called.
/// </summary>
/// <remarks>
/// <para>
/// Package files live per repository, at
/// <c>{DATA_DIR}/repositories/{repositoryId}/{name}-{version}-{architecture}.pkg.tar.{ext}</c>.
/// They cannot share the <c>{DbPath}/sync</c> directory with the <c>.db.tar.gz</c> files, because
/// <c>repo-add</c> records only the basename of a package file: two repositories each holding
/// <c>my-tool-1.0-1-x86_64.pkg.tar.zst</c> would collide.
/// </para>
/// <para>
/// The stored basename is always derived here from package metadata and the sniffed compression of
/// the uploaded bytes. Nothing a client supplied — and not <c>alpm_pkg_get_filename</c>, which
/// reports back the path handed to <c>alpm_pkg_load</c> — may be used to build a path.
/// </para>
/// </remarks>
public interface IPackagePathResolver
{
    /// <summary>
    /// The directory holding a repository's package files.
    /// </summary>
    /// <param name="repositoryId">The repository's id.</param>
    /// <returns>An absolute path, which is not guaranteed to exist yet.</returns>
    string GetRepositoryDirectory(Guid repositoryId);

    /// <summary>
    /// The full path of a stored package file.
    /// </summary>
    /// <param name="repositoryId">The repository the package belongs to.</param>
    /// <param name="fileName">The stored basename, as produced by <see cref="DeriveFileName(string,string,string,PackageCompression)"/>.</param>
    /// <returns>An absolute path under <see cref="GetRepositoryDirectory"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="fileName"/> is not a plain basename.</exception>
    string GetPackageFilePath(Guid repositoryId, string fileName);

    /// <summary>
    /// Classifies an uploaded package file by its leading bytes.
    /// </summary>
    /// <param name="packageContent">The package file's contents. Must be readable and seekable; the position is restored before returning.</param>
    /// <returns>The recognised compression format.</returns>
    /// <exception cref="ArgumentException"><paramref name="packageContent"/> is not readable and seekable.</exception>
    /// <exception cref="Exceptions.UnsupportedPackageCompressionException">The content is not in an allowed compression format.</exception>
    PackageCompression DetectCompression(Stream packageContent);

    /// <summary>
    /// Derives the basename a package is stored under, from metadata libalpm reported and a
    /// compression format sniffed from the uploaded bytes.
    /// </summary>
    /// <param name="name">The package name.</param>
    /// <param name="version">The full <c>[epoch:]pkgver[-pkgrel]</c> version.</param>
    /// <param name="architecture">The architecture the package was built for.</param>
    /// <param name="compression">The compression format detected for the file.</param>
    /// <returns>A basename of the form <c>{name}-{version}-{architecture}.pkg.tar.{ext}</c>.</returns>
    /// <exception cref="Exceptions.InvalidPackageMetadataException">A value does not match the shape pacman defines for it, is too long, or would produce something other than a plain basename.</exception>
    string DeriveFileName(string name, string version, string architecture, PackageCompression compression);

    /// <summary>
    /// Derives the basename a package is stored under, sniffing the compression from the file
    /// itself.
    /// </summary>
    /// <param name="name">The package name.</param>
    /// <param name="version">The full <c>[epoch:]pkgver[-pkgrel]</c> version.</param>
    /// <param name="architecture">The architecture the package was built for.</param>
    /// <param name="packageContent">The package file's contents. Must be readable and seekable; the position is restored before returning.</param>
    /// <returns>A basename of the form <c>{name}-{version}-{architecture}.pkg.tar.{ext}</c>.</returns>
    /// <exception cref="Exceptions.UnsupportedPackageCompressionException">The content is not in an allowed compression format.</exception>
    /// <exception cref="Exceptions.InvalidPackageMetadataException">A value does not match the shape pacman defines for it, is too long, or would produce something other than a plain basename.</exception>
    string DeriveFileName(string name, string version, string architecture, Stream packageContent);
}

namespace PacmanManager.RepoHost.Infrastructure;

/// <summary>
/// The parts of a package file name, as <see cref="IPackagePathResolver.TryParseFileName"/> reads
/// them back out of a basename of the form <c>{name}-{version}-{architecture}.pkg.tar.{ext}</c>.
/// </summary>
/// <param name="Name">The package name.</param>
/// <param name="Version">The full <c>[epoch:]pkgver[-pkgrel]</c> version.</param>
/// <param name="Architecture">The architecture the package was built for, which may be <c>any</c>.</param>
/// <param name="Compression">The compression format the extension names.</param>
public record PackageFileName(string Name, string Version, string Architecture, PackageCompression Compression);

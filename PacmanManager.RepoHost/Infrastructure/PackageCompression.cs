namespace PacmanManager.RepoHost.Infrastructure;

/// <summary>
/// A compression format a pacman package file may use. This is the allowlist: a file whose leading
/// bytes match none of these is not stored, because the extension of the stored file name is
/// derived from this value rather than from anything the client said.
/// </summary>
public enum PackageCompression
{
    /// <summary>
    /// Zstandard, the format <c>makepkg</c> produces by default (<c>.pkg.tar.zst</c>).
    /// </summary>
    Zstandard,

    /// <summary>
    /// XZ (<c>.pkg.tar.xz</c>).
    /// </summary>
    Xz,

    /// <summary>
    /// Gzip (<c>.pkg.tar.gz</c>).
    /// </summary>
    Gzip,

    /// <summary>
    /// Bzip2 (<c>.pkg.tar.bz2</c>).
    /// </summary>
    Bzip2
}

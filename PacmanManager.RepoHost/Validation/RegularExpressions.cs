namespace PacmanManager.RepoHost.Validation;

public class RegularExpressions
{
    public const string RepositoryName = @"^[a-zA-Z0-9@_\+]([-\.]?[a-zA-Z0-9@_\+])*$";
    public const string PackageName = RepositoryName;

    /// <summary>
    /// A full pacman version in <c>[epoch:]pkgver[-pkgrel]</c> form. <c>epoch</c> is a non-negative
    /// integer, <c>pkgver</c> may not contain <c>:</c> or <c>-</c> (they are the separators), and
    /// <c>pkgrel</c> is a number with an optional sub-release, as <c>makepkg</c> allows.
    /// </summary>
    /// <remarks>
    /// Anchored with <c>\A</c>/<c>\z</c> rather than <c>^</c>/<c>$</c> because <c>$</c> also matches
    /// before a trailing newline, and a version is used to derive the name of a file on disk.
    /// </remarks>
    public const string PackageVersion = @"\A(?:\d+:)?[a-zA-Z0-9][a-zA-Z0-9\._\+]*(?:-\d+(?:\.\d+)*)?\z";

    /// <summary>
    /// A package architecture, as it appears in the third segment of a package file name — an
    /// architecture pacman recognises (<c>x86_64</c>, <c>aarch64</c>, <c>any</c>, …).
    /// </summary>
    /// <remarks>
    /// Anchored with <c>\A</c>/<c>\z</c> for the same reason as <see cref="PackageVersion"/>: it is
    /// used to derive the name of a file on disk, and <c>$</c> would also match before a trailing
    /// newline.
    /// </remarks>
    public const string PackageArchitecture = @"\A[a-zA-Z0-9][a-zA-Z0-9_\.\+]*\z";
}

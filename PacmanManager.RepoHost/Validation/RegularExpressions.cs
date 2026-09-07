using System.ComponentModel.DataAnnotations;

namespace PacmanManager.RepoHost.Validation;

/// <summary>
/// Shared regular expressions for validating pacman identifiers.
/// </summary>
public class RegularExpressions
{
    /// <summary>
    /// A repository name: alphanumerics, <c>@</c>, <c>_</c> and <c>+</c>, with single <c>-</c> or
    /// <c>.</c> separators between them, and never leading with a separator.
    /// </summary>
    /// <remarks>
    /// Anchored with <c>\A</c>/<c>\z</c> rather than <c>^</c>/<c>$</c>, for the same reason as
    /// <see cref="PackageVersion"/>: <c>$</c> also matches immediately before a trailing newline, so
    /// <c>^…$</c> accepts <c>"core\n"</c> as a valid name. <see cref="RegularExpressionAttribute"/>
    /// happens to reject that anyway, because it additionally requires the match to span the whole
    /// value, but a direct <c>Regex.IsMatch</c> call gets no such help — and both of these names are
    /// used to build paths on disk.
    /// </remarks>
    public const string RepositoryName = @"\A[a-zA-Z0-9@_\+]([-\.]?[a-zA-Z0-9@_\+])*\z";

    /// <summary>
    /// A package name, which pacman constrains exactly as it does a repository name.
    /// </summary>
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

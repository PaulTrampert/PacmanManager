namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The properties a package listing may be ordered by.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Name"/> is first because <see cref="SortOptions{TSortFields}"/> takes the first member
/// as its default, so reordering this enum silently changes what every unsorted request returns. A
/// listing nobody has ordered comes back by package name, which is the order someone browsing a
/// repository expects and the one that makes paging through a large repository legible.
/// </para>
/// <para>
/// Each member also declares which way round it runs when the caller omits <c>direction</c>: the
/// dates and the size default to biggest and newest first, and <see cref="Name"/> to A→Z. See
/// <see cref="DefaultSortDirectionAttribute"/>.
/// </para>
/// <para>
/// There is deliberately no member for the package version. A listing spans packages, and comparing
/// one package's version to another's answers no question anyone has; it would also be wrong on its
/// own terms, since the version is stored as text and a lexicographic ordering puts
/// <c>1.10.0-1</c> before <c>1.9.0-1</c>. A repository holds exactly one version of each package,
/// so there is no within-package ordering to recover either.
/// </para>
/// </remarks>
public enum PackageSortField
{
    /// <summary>The package name.</summary>
    [DefaultSortDirection(SortDirection.Ascending)]
    Name,

    /// <summary>When the package was last published.</summary>
    Updated,

    /// <summary>When the package first appeared in its repository.</summary>
    Created,

    /// <summary>The size the package occupies once installed.</summary>
    InstalledSize,
}

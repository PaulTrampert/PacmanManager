using System.Linq.Expressions;
using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// A package published into a hosted repository, as callers see it.
/// </summary>
/// <remarks>
/// <para>
/// The wire model keeps the unprefixed name for the same reason <see cref="Repository"/> does: the
/// <c>Pacman</c> prefix on <see cref="PacmanPackage"/> distinguishes the storage type, and this is
/// the one callers read about.
/// </para>
/// <para>
/// Every value here was extracted from the uploaded package file by libalpm, or computed over its
/// bytes, rather than asserted by whoever published it.
/// </para>
/// </remarks>
public record Package
{
    /// <summary>
    /// Internal database Id for the package.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Id of the repository this package belongs to.
    /// </summary>
    public required Guid RepositoryId { get; init; }

    /// <summary>
    /// The user who most recently published this package. It records who, and confers no rights of
    /// its own: publishing, replacing and deleting are decided by the permission held over the
    /// repository.
    /// </summary>
    public required PublicUserInfo Publisher { get; init; }

    /// <summary>
    /// Package name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full package version, in pacman's <c>[epoch:]pkgver[-pkgrel]</c> form.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Package description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The package base (<c>pkgbase</c>) this package was built from.
    /// </summary>
    public string? Base { get; init; }

    /// <summary>
    /// Upstream URL for the packaged software.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Architecture the package was built for, which is not always the repository's.
    /// </summary>
    public required string Architecture { get; init; }

    /// <summary>
    /// Who built the package, typically <c>Name &lt;email&gt;</c>.
    /// </summary>
    public string? Packager { get; init; }

    /// <summary>
    /// Basename of the stored package file.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Size of the package file in bytes.
    /// </summary>
    public long CompressedSize { get; init; }

    /// <summary>
    /// Size the package occupies once installed, in bytes.
    /// </summary>
    public long InstalledSize { get; init; }

    /// <summary>
    /// When the package was built.
    /// </summary>
    public DateTimeOffset BuildDate { get; init; }

    /// <summary>
    /// SHA-256 checksum of the published bytes, for a caller that wants to verify a download.
    /// </summary>
    /// <remarks>
    /// The entity also records an MD5 checksum — it falls out of the same pass over the upload, and
    /// older tooling still asks for MD5 — but it is not published here: pacman 7's <c>repo-add</c>
    /// records only <c>%SHA256SUM%</c>, and MD5 is not a checksum anyone should be verifying
    /// against today.
    /// </remarks>
    public required string Sha256Sum { get; init; }

    /// <summary>
    /// Licenses the package is distributed under.
    /// </summary>
    public IEnumerable<string> Licenses { get; init; } = [];

    /// <summary>
    /// Groups the package belongs to.
    /// </summary>
    public IEnumerable<string> Groups { get; init; } = [];

    /// <summary>
    /// Virtual packages this package provides, in pacman's own spelling (e.g. <c>foo=1.2</c>).
    /// </summary>
    public IEnumerable<string> Provides { get; init; } = [];

    /// <summary>
    /// Packages this package replaces, in pacman's own spelling.
    /// </summary>
    public IEnumerable<string> Replaces { get; init; } = [];

    /// <summary>
    /// Runtime dependencies, in pacman's own spelling (e.g. <c>foo&gt;=1.2</c>).
    /// </summary>
    public IEnumerable<string> Depends { get; init; } = [];

    /// <summary>
    /// Optional dependencies, in pacman's own spelling (e.g. <c>bar: reason</c>).
    /// </summary>
    public IEnumerable<string> OptDepends { get; init; } = [];

    /// <summary>
    /// Build time dependencies, in pacman's own spelling.
    /// </summary>
    public IEnumerable<string> MakeDepends { get; init; } = [];

    /// <summary>
    /// Check time dependencies, in pacman's own spelling.
    /// </summary>
    public IEnumerable<string> CheckDepends { get; init; } = [];

    /// <summary>
    /// Packages this one conflicts with, in pacman's own spelling.
    /// </summary>
    public IEnumerable<string> Conflicts { get; init; } = [];

    /// <summary>
    /// When this package first appeared in this repository.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When this package was last published.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Projects the underlying db object onto this model inside a query.
    /// </summary>
    /// <remarks>
    /// Following <see cref="Repository.Projection"/>: it pulls the publisher in the same round trip,
    /// so callers cannot forget to include the navigation and hit a null reference, and it never
    /// fetches columns the model does not expose.
    /// </remarks>
    public static Expression<Func<PacmanPackage, Package>> Projection => package => new Package
    {
        Id = package.Id,
        RepositoryId = package.RepositoryId,
        Publisher = new PublicUserInfo
        {
            Id = package.Publisher.Id,
            DisplayName = package.Publisher.DisplayName,
        },
        Name = package.Name,
        Version = package.Version,
        Description = package.Description,
        Base = package.Base,
        Url = package.Url,
        Architecture = package.Architecture,
        Packager = package.Packager,
        FileName = package.FileName,
        CompressedSize = package.CompressedSize,
        InstalledSize = package.InstalledSize,
        BuildDate = package.BuildDate,
        Sha256Sum = package.Sha256Sum,
        Licenses = package.Licenses,
        Groups = package.Groups,
        Provides = package.Provides,
        Replaces = package.Replaces,
        Depends = package.Depends,
        OptDepends = package.OptDepends,
        MakeDepends = package.MakeDepends,
        CheckDepends = package.CheckDepends,
        Conflicts = package.Conflicts,
        CreatedAt = package.CreatedAt,
        UpdatedAt = package.UpdatedAt,
    };

    /// <summary>
    /// Creates the API model from an entity already in hand.
    /// </summary>
    /// <param name="package">
    /// The entity to project. Its <see cref="PacmanPackage.Publisher"/> navigation must be
    /// populated, since the model publishes a summary of the publisher rather than an id.
    /// </param>
    /// <returns>The package as callers see it.</returns>
    /// <remarks>
    /// <see cref="Projection"/> is the one to reach for on a read path, because it pulls the
    /// publisher in the same round trip. This exists for the write path, which has just built or
    /// updated the entity and would otherwise re-read a row it already holds.
    /// </remarks>
    public static Package FromPacmanPackage(PacmanPackage package) => new()
    {
        Id = package.Id,
        RepositoryId = package.RepositoryId,
        Publisher = PublicUserInfo.FromUser(package.Publisher),
        Name = package.Name,
        Version = package.Version,
        Description = package.Description,
        Base = package.Base,
        Url = package.Url,
        Architecture = package.Architecture,
        Packager = package.Packager,
        FileName = package.FileName,
        CompressedSize = package.CompressedSize,
        InstalledSize = package.InstalledSize,
        BuildDate = package.BuildDate,
        Sha256Sum = package.Sha256Sum,
        Licenses = package.Licenses,
        Groups = package.Groups,
        Provides = package.Provides,
        Replaces = package.Replaces,
        Depends = package.Depends,
        OptDepends = package.OptDepends,
        MakeDepends = package.MakeDepends,
        CheckDepends = package.CheckDepends,
        Conflicts = package.Conflicts,
        CreatedAt = package.CreatedAt,
        UpdatedAt = package.UpdatedAt,
    };
}

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace PacmanManager.Entities;

/// <summary>
/// A package published into a hosted pacman repository. Every column is metadata libalpm reported
/// for the uploaded file, or a checksum computed over its bytes, rather than anything a client
/// asserted.
/// </summary>
/// <remarks>
/// The unique index on <c>(RepositoryId, Name)</c> is the model, not a simplification: a repository
/// holds exactly one version of a package, because pacman rolls forward and has no rollback. It is
/// what makes <c>(repositoryId, name)</c> a usable route key and what the publish upsert relies on.
/// </remarks>
[Index(nameof(RepositoryId), nameof(Name), IsUnique = true)]
public record PacmanPackage
{
    /// <summary>
    /// Internal database Id for the package.
    /// </summary>
    [Key]
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// Id of the repository this package belongs to.
    /// </summary>
    [Required]
    public Guid RepositoryId { get; set; }

    /// <summary>
    /// The repository this package belongs to.
    /// </summary>
    public virtual PacmanRepository Repository { get; set; }

    /// <summary>
    /// Id of the user who most recently published this package. It records who, and confers no
    /// rights of its own: publish, replace and delete are all decided by the permission held over
    /// the repository.
    /// </summary>
    [Required]
    public Guid PublisherId { get; set; }

    /// <summary>
    /// The user who most recently published this package.
    /// </summary>
    public virtual User Publisher { get; set; }

    /// <summary>
    /// Package name, as reported by libalpm.
    /// </summary>
    [Required]
    [MaxLength(PackageValidationConstants.NameMaxLength)]
    [MinLength(PackageValidationConstants.NameMinLength)]
    public required string Name { get; set; }

    /// <summary>
    /// Full package version, in pacman's <c>[epoch:]pkgver[-pkgrel]</c> form.
    /// </summary>
    [Required]
    [MaxLength(PackageValidationConstants.VersionMaxLength)]
    public required string Version { get; set; }

    /// <summary>
    /// Package description.
    /// </summary>
    [MaxLength(PackageValidationConstants.DescriptionMaxLength)]
    public string? Description { get; set; }

    /// <summary>
    /// The package base (<c>pkgbase</c>) this package was built from.
    /// </summary>
    [MaxLength(PackageValidationConstants.BaseMaxLength)]
    public string? Base { get; set; }

    /// <summary>
    /// Upstream URL for the packaged software.
    /// </summary>
    [MaxLength(PackageValidationConstants.UrlMaxLength)]
    public string? Url { get; set; }

    /// <summary>
    /// Architecture the package was built for, which is not always the repository's.
    /// </summary>
    [Required]
    [MaxLength(PackageValidationConstants.ArchitectureMaxLength)]
    public required string Architecture { get; set; }

    /// <summary>
    /// Who built the package, typically <c>Name &lt;email&gt;</c>.
    /// </summary>
    [MaxLength(PackageValidationConstants.PackagerMaxLength)]
    public string? Packager { get; set; }

    /// <summary>
    /// Basename of the stored package file. Derived from the package metadata, never taken from
    /// libalpm's own file name or from the client.
    /// </summary>
    [Required]
    [MaxLength(PackageValidationConstants.FileNameMaxLength)]
    public required string FileName { get; set; }

    /// <summary>
    /// Size of the package file in bytes.
    /// </summary>
    public long CompressedSize { get; set; }

    /// <summary>
    /// Size the package occupies once installed, in bytes.
    /// </summary>
    public long InstalledSize { get; set; }

    /// <summary>
    /// When the package was built.
    /// </summary>
    public DateTimeOffset BuildDate { get; set; }

    /// <summary>
    /// SHA-256 checksum of the uploaded bytes, computed rather than read from libalpm.
    /// </summary>
    [Required]
    [MaxLength(PackageValidationConstants.Sha256SumLength)]
    public required string Sha256Sum { get; set; }

    /// <summary>
    /// MD5 checksum of the uploaded bytes, computed rather than read from libalpm. Pacman 7's
    /// <c>repo-add</c> records only <c>%SHA256SUM%</c>; this is kept because the same pass produces
    /// it for free and older tooling still asks for MD5.
    /// </summary>
    [Required]
    [MaxLength(PackageValidationConstants.Md5SumLength)]
    public required string Md5Sum { get; set; }

    /// <summary>
    /// Licenses the package is distributed under.
    /// </summary>
    public IEnumerable<string> Licenses { get; set; } = [];

    /// <summary>
    /// Groups the package belongs to.
    /// </summary>
    public IEnumerable<string> Groups { get; set; } = [];

    /// <summary>
    /// Virtual packages this package provides, in pacman's own spelling (e.g. <c>foo=1.2</c>).
    /// </summary>
    public IEnumerable<string> Provides { get; set; } = [];

    /// <summary>
    /// Packages this package replaces, in pacman's own spelling.
    /// </summary>
    public IEnumerable<string> Replaces { get; set; } = [];

    /// <summary>
    /// Runtime dependencies, in pacman's own spelling (e.g. <c>foo&gt;=1.2</c>).
    /// </summary>
    public IEnumerable<string> Depends { get; set; } = [];

    /// <summary>
    /// Optional dependencies, in pacman's own spelling (e.g. <c>bar: reason</c>).
    /// </summary>
    public IEnumerable<string> OptDepends { get; set; } = [];

    /// <summary>
    /// Build time dependencies, in pacman's own spelling.
    /// </summary>
    public IEnumerable<string> MakeDepends { get; set; } = [];

    /// <summary>
    /// Check time dependencies, in pacman's own spelling.
    /// </summary>
    public IEnumerable<string> CheckDepends { get; set; } = [];

    /// <summary>
    /// Packages this one conflicts with, in pacman's own spelling.
    /// </summary>
    public IEnumerable<string> Conflicts { get; set; } = [];

    /// <summary>
    /// When this package first appeared in this repository.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When this package was last published. Backs the <c>updatedSince</c> filter.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

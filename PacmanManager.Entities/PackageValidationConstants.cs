namespace PacmanManager.Entities;

/// <summary>
/// Validation limits for <see cref="PacmanPackage"/>. Shared so that the entity, the API models and
/// their tests all agree on the same numbers instead of repeating literals at each use site.
/// </summary>
public static class PackageValidationConstants
{
    /// <summary>
    /// Maximum length of a package name.
    /// </summary>
    public const int NameMaxLength = 255;

    /// <summary>
    /// Minimum length of a package name.
    /// </summary>
    public const int NameMinLength = 1;

    /// <summary>
    /// Maximum length of a full <c>[epoch:]pkgver[-pkgrel]</c> version string.
    /// </summary>
    public const int VersionMaxLength = 255;

    /// <summary>
    /// Maximum length of a package description.
    /// </summary>
    public const int DescriptionMaxLength = 1024;

    /// <summary>
    /// Maximum length of a package base (<c>pkgbase</c>) name.
    /// </summary>
    public const int BaseMaxLength = 255;

    /// <summary>
    /// Maximum length of an upstream URL.
    /// </summary>
    public const int UrlMaxLength = 2048;

    /// <summary>
    /// Maximum length of a package architecture.
    /// </summary>
    public const int ArchitectureMaxLength = PacmanRepositoryValidationConstants.ArchitectureMaxLength;

    /// <summary>
    /// Maximum length of the packager string (typically <c>Name &lt;email&gt;</c>).
    /// </summary>
    public const int PackagerMaxLength = 255;

    /// <summary>
    /// Maximum length of the stored package file name. This is a basename, never a path.
    /// </summary>
    public const int FileNameMaxLength = 512;

    /// <summary>
    /// Length of a hex encoded SHA-256 checksum.
    /// </summary>
    public const int Sha256SumLength = 64;

    /// <summary>
    /// Length of a hex encoded MD5 checksum.
    /// </summary>
    public const int Md5SumLength = 32;
}

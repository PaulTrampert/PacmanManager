using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Startup.LibAlpm;
using PacmanManager.RepoHost.Validation;

namespace PacmanManager.RepoHost.Infrastructure;

/// <summary>
/// The default <see cref="IPackagePathResolver"/>, laying package files out under
/// <c>{DATA_DIR}/repositories/{repositoryId}</c>.
/// </summary>
/// <param name="settings">The pacman configuration, which supplies <c>DATA_DIR</c>.</param>
public class PackagePathResolver(IOptions<PacmanConfigSettings> settings) : IPackagePathResolver
{
    /// <summary>
    /// The fixed part of every package file name, between the metadata and the compression
    /// extension.
    /// </summary>
    private const string PackageFileSuffix = ".pkg.tar.";

    private static readonly Regex NamePattern = new(RegularExpressions.PackageName, RegexOptions.CultureInvariant);
    private static readonly Regex VersionPattern = new(RegularExpressions.PackageVersion, RegexOptions.CultureInvariant);

    private static readonly Regex ArchitecturePattern =
        new(RegularExpressions.PackageArchitecture, RegexOptions.CultureInvariant);

    /// <inheritdoc/>
    public string GetRepositoryDirectory(Guid repositoryId) =>
        Path.Combine(settings.Value.RepositoriesDir, repositoryId.ToString());

    /// <inheritdoc/>
    public string GetPackageFilePath(Guid repositoryId, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!IsPlainFileName(fileName))
        {
            throw new ArgumentException(
                $"'{fileName}' is not a plain file name; a package file path is only ever composed from a basename.",
                nameof(fileName));
        }

        return Path.Combine(GetRepositoryDirectory(repositoryId), fileName);
    }

    /// <inheritdoc/>
    public PackageCompression DetectCompression(Stream packageContent)
    {
        ArgumentNullException.ThrowIfNull(packageContent);

        if (!packageContent.CanRead || !packageContent.CanSeek)
        {
            throw new ArgumentException("The package content must be readable and seekable.", nameof(packageContent));
        }

        var originalPosition = packageContent.Position;
        try
        {
            packageContent.Position = 0;
            var header = new byte[PackageCompressions.MaxMagicNumberLength];
            var read = packageContent.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);

            if (!PackageCompressions.TryDetect(header.AsSpan(0, read), out var compression))
            {
                throw new UnsupportedPackageCompressionException();
            }

            return compression;
        }
        finally
        {
            packageContent.Position = originalPosition;
        }
    }

    /// <inheritdoc/>
    public string DeriveFileName(string name, string version, string architecture, Stream packageContent) =>
        DeriveFileName(name, version, architecture, DetectCompression(packageContent));

    /// <inheritdoc/>
    public string DeriveFileName(string name, string version, string architecture, PackageCompression compression)
    {
        // Validate before formatting. The metadata is whatever libalpm read out of a file the
        // caller chose, so none of it may reach a path without having been matched against the
        // shape pacman defines for it first.
        Validate(nameof(name), name, NamePattern, PackageValidationConstants.NameMaxLength);
        Validate(nameof(version), version, VersionPattern, PackageValidationConstants.VersionMaxLength);
        Validate(nameof(architecture), architecture, ArchitecturePattern,
            PackageValidationConstants.ArchitectureMaxLength);

        var fileName = $"{name}-{version}-{architecture}{PackageFileSuffix}{compression.ToFileExtension()}";

        // Belt and braces: whatever the patterns allowed, the result has to be a basename. A
        // regression in any of them turns into a rejected upload here rather than a write outside
        // the repository's directory.
        if (!IsPlainFileName(fileName))
        {
            throw new InvalidPackageMetadataException(nameof(name),
                "the derived file name is not a plain file name.");
        }

        if (fileName.Length > PackageValidationConstants.FileNameMaxLength)
        {
            throw new InvalidPackageMetadataException(nameof(name),
                $"the derived file name is longer than {PackageValidationConstants.FileNameMaxLength} characters.");
        }

        return fileName;
    }

    /// <summary>
    /// Rejects a metadata value that does not match its pattern, is empty, is too long, or carries
    /// anything that has no business in a file name.
    /// </summary>
    private static void Validate(string field, string value, Regex pattern, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidPackageMetadataException(field, "it is empty.");
        }

        if (value.Length > maxLength)
        {
            throw new InvalidPackageMetadataException(field, $"it is longer than {maxLength} characters.");
        }

        // Checked separately from the pattern because RegularExpressions.PackageName is anchored
        // with ^/$, and $ also matches immediately before a trailing newline.
        if (!IsPlainFileName(value))
        {
            throw new InvalidPackageMetadataException(field,
                "it contains a path separator or a character that is not valid in a file name.");
        }

        if (!pattern.IsMatch(value))
        {
            throw new InvalidPackageMetadataException(field, "it does not match the form pacman defines for it.");
        }
    }

    /// <summary>
    /// Whether a value is a plain basename: no path separator, no control character, and not a
    /// relative directory reference.
    /// </summary>
    /// <remarks>
    /// The separators are spelled out rather than taken from <see cref="Path.GetInvalidFileNameChars"/>,
    /// which is platform dependent: on Windows it also rejects <c>:</c>, and an epoch version
    /// (<c>2:1.4.2-1</c>) legitimately contains one.
    /// </remarks>
    private static bool IsPlainFileName(string value) =>
        value.Length > 0
        && value != "."
        && value != ".."
        && !value.Contains('/')
        && !value.Contains('\\')
        && !value.Any(char.IsControl);
}

using PacmanManager.RepoHost.Infrastructure;

namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Thrown when an uploaded file's leading bytes match none of the compression formats in the
/// <see cref="PackageCompressions"/> allowlist.
/// </summary>
/// <remarks>
/// The extension of a stored package file comes from the sniffed compression, so an unrecognised
/// format has no name to be stored under and the upload is refused rather than guessed at.
/// </remarks>
public class UnsupportedPackageCompressionException()
    : InvalidPackageException(
        "The uploaded file is not compressed with a supported format. Supported formats are zstd, xz, gzip and bzip2.");

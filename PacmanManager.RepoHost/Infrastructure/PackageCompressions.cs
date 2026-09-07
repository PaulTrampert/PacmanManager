namespace PacmanManager.RepoHost.Infrastructure;

/// <summary>
/// The magic-number allowlist behind <see cref="PackageCompression"/>, and the file extension each
/// format contributes to a derived package file name.
/// </summary>
/// <remarks>
/// The compression of an uploaded package is sniffed from its own bytes rather than read from a
/// client supplied name, so that no part of a stored path can be chosen by a caller.
/// </remarks>
public static class PackageCompressions
{
    private static readonly (PackageCompression Compression, byte[] Magic)[] MagicNumbers =
    [
        // Zstandard frame magic, little endian 0xFD2FB528.
        (PackageCompression.Zstandard, [0x28, 0xB5, 0x2F, 0xFD]),
        // XZ stream header: 0xFD '7' 'z' 'X' 'Z' 0x00.
        (PackageCompression.Xz, [0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00]),
        // Gzip member header.
        (PackageCompression.Gzip, [0x1F, 0x8B]),
        // Bzip2 signature "BZh".
        (PackageCompression.Bzip2, [0x42, 0x5A, 0x68])
    ];

    /// <summary>
    /// The number of leading bytes that have to be read from a file to classify it.
    /// </summary>
    public static int MaxMagicNumberLength { get; } = MagicNumbers.Max(m => m.Magic.Length);

    /// <summary>
    /// Attempts to classify a file from its leading bytes.
    /// </summary>
    /// <param name="header">The first <see cref="MaxMagicNumberLength"/> bytes of the file, or fewer if the file is shorter.</param>
    /// <param name="compression">The recognised compression format, when this returns true.</param>
    /// <returns>True when the header matches an allowed compression format; otherwise, false.</returns>
    public static bool TryDetect(ReadOnlySpan<byte> header, out PackageCompression compression)
    {
        foreach (var (candidate, magic) in MagicNumbers)
        {
            if (header.Length >= magic.Length && header[..magic.Length].SequenceEqual(magic))
            {
                compression = candidate;
                return true;
            }
        }

        compression = default;
        return false;
    }

    /// <summary>
    /// The file extension a compression format contributes, without a leading dot.
    /// </summary>
    /// <param name="compression">The compression format.</param>
    /// <returns>The extension, e.g. <c>zst</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="PackageCompression"/>.</exception>
    public static string ToFileExtension(this PackageCompression compression) => compression switch
    {
        PackageCompression.Zstandard => "zst",
        PackageCompression.Xz => "xz",
        PackageCompression.Gzip => "gz",
        PackageCompression.Bzip2 => "bz2",
        _ => throw new ArgumentOutOfRangeException(nameof(compression), compression,
            "Unknown package compression format.")
    };
}

namespace PacmanManager.Packages;

/// <summary>
/// A package file received for publishing, together with what one pass over its bytes measured.
/// </summary>
/// <remarks>
/// The checksums are computed over the bytes received rather than read back from libalpm on
/// purpose. libalpm fills its checksum fields from a sync database entry, and a package loaded off
/// disk has no such entry, so both come back null — reading them would store a null for every
/// package published. The SHA-256 is the one that has to match the repository database, since
/// <c>repo-add</c> records it there and a pacman client verifies against it. Pacman 7's
/// <c>repo-add</c> no longer writes <c>%MD5SUM%</c> at all; the MD5 is kept because the same pass
/// produces it for free and older tooling still asks for it.
/// </remarks>
/// <param name="Path">Where the received file is. It must be on the same filesystem as the repository tree.</param>
/// <param name="Sha256Sum">Hex encoded SHA-256 of the file's bytes.</param>
/// <param name="Md5Sum">Hex encoded MD5 of the same bytes.</param>
/// <param name="ByteCount">The length of the file in bytes.</param>
public sealed record PackageUpload(string Path, string Sha256Sum, string Md5Sum, long ByteCount);

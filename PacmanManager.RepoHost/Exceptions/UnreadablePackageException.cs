namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Thrown when libalpm could not read the uploaded file as a package at all — it is not an archive,
/// it carries no <c>.PKGINFO</c>, or it is truncated.
/// </summary>
/// <remarks>
/// Every field stored about a package is read out of the file, so a file libalpm cannot open has
/// nothing to store. That is a statement about what the caller sent, which is why this is an
/// <see cref="InvalidPackageException"/> and not the server error the underlying libalpm failure
/// would otherwise surface as.
/// </remarks>
/// <param name="inner">The failure libalpm reported.</param>
public class UnreadablePackageException(Exception inner)
    : InvalidPackageException("The uploaded file could not be read as a pacman package.", inner);

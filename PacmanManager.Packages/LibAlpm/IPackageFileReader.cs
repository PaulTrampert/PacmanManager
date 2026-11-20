using LibAlpmSharp;
using PacmanManager.Packages.Exceptions;

namespace PacmanManager.Packages.LibAlpm;

/// <summary>
/// Reads a package file's metadata through libalpm.
/// </summary>
public interface IPackageFileReader
{
    /// <summary>
    /// Loads the package file at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The package file to read.</param>
    /// <returns>The package libalpm read, which the caller disposes.</returns>
    /// <exception cref="UnreadablePackageException">libalpm could not read the file as a package.</exception>
    IPackage Read(string path);
}

using LibAlpmSharp;
using PacmanManager.Packages.Exceptions;

namespace PacmanManager.Packages.LibAlpm;

/// <summary>
/// The default <see cref="IPackageFileReader"/>, restating a libalpm failure as the client error it
/// is.
/// </summary>
/// <remarks>
/// libalpm arrives as a <see cref="Lazy{T}"/> because only reading a package file needs it.
/// Constructing an <see cref="ILibAlpm"/> regenerates and parses the pacman configuration, expands
/// the per-repository <c>.conf</c> glob and calls <c>alpm_initialize</c>, and every request that
/// resolves a consumer of this reader would otherwise pay for that — including RepoHost's anonymous
/// read routes, which never load a package file. Worse, it made one malformed <c>.conf</c> enough to
/// turn every listing into a 500. <see cref="Read"/> is the only member allowed to force the value.
/// </remarks>
/// <param name="libAlpm">The libalpm handle, created only when a package is first read.</param>
public class PackageFileReader(Lazy<ILibAlpm> libAlpm) : IPackageFileReader
{
    /// <inheritdoc />
    public IPackage Read(string path)
    {
        try
        {
            return libAlpm.Value.LoadPackageFile(path);
        }
        catch (AlpmException e)
        {
            throw new UnreadablePackageException(e);
        }
    }
}

using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Thrown when an uploaded package was built for an architecture the repository does not serve.
/// </summary>
/// <remarks>
/// A repository serves the architectures it supports, plus <see cref="Architectures.Any"/> for
/// packages that contain nothing architecture specific. Accepting anything else would put a package
/// into a database no pacman client reading it could install, so the upload is refused rather than
/// stored.
/// </remarks>
/// <param name="packageArchitecture">The architecture the package was built for.</param>
/// <param name="repositoryArchitectures">The architectures the repository supports.</param>
public class PackageArchitectureMismatchException(
    string packageArchitecture,
    IEnumerable<string> repositoryArchitectures)
    : InvalidPackageException(
        $"The package was built for '{packageArchitecture}', but this repository serves "
        + string.Join(", ", repositoryArchitectures.Append(Architectures.Any).Select(a => $"'{a}'")) + ".")
{
    /// <summary>
    /// The architecture the package was built for.
    /// </summary>
    public string PackageArchitecture { get; } = packageArchitecture;

    /// <summary>
    /// The architectures the repository supports.
    /// </summary>
    public IEnumerable<string> RepositoryArchitectures { get; } = repositoryArchitectures.ToList();
}

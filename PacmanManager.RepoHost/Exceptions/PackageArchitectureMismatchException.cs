namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Thrown when an uploaded package was built for an architecture the repository does not serve.
/// </summary>
/// <remarks>
/// A repository serves exactly one architecture, plus <see cref="AnyArchitecture"/> for packages
/// that contain nothing architecture specific. Accepting anything else would put a package into a
/// database no pacman client reading it could install, so the upload is refused rather than
/// stored.
/// </remarks>
/// <param name="packageArchitecture">The architecture the package was built for.</param>
/// <param name="repositoryArchitecture">The architecture the repository serves.</param>
public class PackageArchitectureMismatchException(string packageArchitecture, string repositoryArchitecture)
    : InvalidPackageException(
        $"The package was built for '{packageArchitecture}', but this repository serves "
        + $"'{repositoryArchitecture}' and '{AnyArchitecture}'.")
{
    /// <summary>
    /// The architecture every repository accepts alongside its own, for packages that contain
    /// nothing architecture specific.
    /// </summary>
    public const string AnyArchitecture = "any";

    /// <summary>
    /// The architecture the package was built for.
    /// </summary>
    public string PackageArchitecture { get; } = packageArchitecture;

    /// <summary>
    /// The architecture the repository serves.
    /// </summary>
    public string RepositoryArchitecture { get; } = repositoryArchitecture;
}

namespace PacmanManager.Entities;

/// <summary>
/// Validation limits for <see cref="PacmanRepository"/>, shared so that the entity, the API models
/// and their tests agree on the same values.
/// </summary>
public static class PacmanRepositoryValidationConstants
{
    /// <summary>
    /// Maximum length of a repository name.
    /// </summary>
    public const int NameMaxLength = 255;

    /// <summary>
    /// Minimum length of a repository name.
    /// </summary>
    public const int NameMinLength = 3;

    /// <summary>
    /// Maximum length of an architecture name.
    /// </summary>
    public const int ArchitectureMaxLength = 255;

    /// <summary>
    /// The machine architectures a repository may support. <see cref="Architectures.Any"/> is
    /// deliberately absent: it describes a package, never a repository.
    /// </summary>
    public static readonly IEnumerable<string> SupportedArchitectures =
        Architectures.All.Except([Architectures.Any]);

    /// <summary>
    /// The architectures a repository supports when a request does not say. Every allowed
    /// architecture, rather than a single one, so that an omitted request does not silently narrow
    /// to whichever architecture happened to be the default when it was added.
    /// </summary>
    public static readonly IEnumerable<string> DefaultArchitecture = SupportedArchitectures;
}

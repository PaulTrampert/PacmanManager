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
    /// The architecture a repository supports when a request does not say.
    /// </summary>
    public const string DefaultArchitecture = "x86_64";

    /// <summary>
    /// The machine architectures a repository may support. <c>any</c> is deliberately absent: it
    /// describes a package, never a repository.
    /// </summary>
    public static readonly IReadOnlyCollection<string> SupportedArchitectures = [DefaultArchitecture];
}

using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// A machine architecture a repository may support, with a label for showing it to people.
/// </summary>
public record Architecture
{
    /// <summary>
    /// The architecture's name as pacman and the API know it, such as <c>x86_64</c>.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// The architecture's friendly name followed by its value in parentheses, such as
    /// <c>64-bit Intel/AMD (x86_64)</c>.
    /// </summary>
    public required string DisplayValue { get; init; }

    /// <summary>
    /// The architectures a repository may support, in the order of
    /// <see cref="PacmanRepositoryValidationConstants.SupportedArchitectures"/>.
    /// </summary>
    public static IEnumerable<Architecture> Supported =>
        PacmanRepositoryValidationConstants.SupportedArchitectures.Select(FromValue);

    /// <summary>
    /// Describes a supported architecture by its value.
    /// </summary>
    /// <param name="value">One of <see cref="PacmanRepositoryValidationConstants.SupportedArchitectures"/>.</param>
    /// <returns>The architecture, labelled with its friendly name.</returns>
    /// <exception cref="KeyNotFoundException">
    /// <paramref name="value"/> has no entry in
    /// <see cref="PacmanRepositoryValidationConstants.ArchitectureFriendlyNames"/>.
    /// </exception>
    public static Architecture FromValue(string value) => new()
    {
        Value = value,
        DisplayValue = $"{PacmanRepositoryValidationConstants.ArchitectureFriendlyNames[value]} ({value})",
    };
}

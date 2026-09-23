using System.Collections;
using System.ComponentModel.DataAnnotations;
using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Validation;

/// <summary>
/// Requires an architecture, or every architecture in a collection, to be one a repository may
/// support: a member of <see cref="PacmanRepositoryValidationConstants.SupportedArchitectures"/>.
/// </summary>
/// <remarks>
/// <para>
/// The allowed set is a shared constant rather than a list repeated in each attribute, so that the
/// request, the filter and the key cannot drift apart when an architecture is added. <c>any</c> is
/// never a member: it describes a package, not a repository.
/// </para>
/// <para>
/// A null value is valid, so that an optional filter criterion can be left unset; pair this with
/// <see cref="RequiredAttribute"/> where a value is mandatory. A collection must also be non-empty
/// unless <see cref="AllowEmpty"/> is set, because a repository that supports nothing can serve
/// nothing.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SupportedArchitectureAttribute : ValidationAttribute
{
    /// <summary>
    /// Whether an empty collection is valid. Defaults to <c>false</c>.
    /// </summary>
    public bool AllowEmpty { get; init; }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var memberNames = validationContext.MemberName is { } member ? new[] { member } : null;

        switch (value)
        {
            case null:
                return ValidationResult.Success;

            case string architecture:
                return IsSupported(architecture)
                    ? ValidationResult.Success
                    : new ValidationResult(Unsupported(architecture), memberNames);

            case IEnumerable collection:
            {
                var architectures = collection.Cast<object?>().ToList();
                if (architectures.Count == 0 && !AllowEmpty)
                {
                    return new ValidationResult(
                        "At least one architecture is required; a repository that supports nothing can serve nothing.",
                        memberNames);
                }

                foreach (var element in architectures)
                {
                    if (element is not string architecture || !IsSupported(architecture))
                    {
                        return new ValidationResult(Unsupported(element as string), memberNames);
                    }
                }

                return ValidationResult.Success;
            }

            default:
                return new ValidationResult(Unsupported(value.ToString()), memberNames);
        }
    }

    private static bool IsSupported(string architecture) =>
        PacmanRepositoryValidationConstants.SupportedArchitectures.Contains(architecture, StringComparer.Ordinal);

    private static string Unsupported(string? architecture) =>
        $"'{architecture}' is not a supported architecture. A repository may support: "
        + string.Join(", ", PacmanRepositoryValidationConstants.SupportedArchitectures) + ".";
}

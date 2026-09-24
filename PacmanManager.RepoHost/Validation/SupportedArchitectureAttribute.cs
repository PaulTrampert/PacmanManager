using System.Collections;
using System.ComponentModel.DataAnnotations;
using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Validation;

/// <summary>
/// Requires an architecture, or every architecture in a collection, to be one a repository may
/// support: a member of <see cref="PacmanRepositoryValidationConstants.SupportedArchitectures"/>,
/// plus <see cref="Architectures.Any"/> when <see cref="AllowAny"/> is set.
/// </summary>
/// <remarks>
/// <para>
/// The allowed set is a shared constant rather than a list repeated in each attribute, so that the
/// request, the filter and the key cannot drift apart when an architecture is added.
/// </para>
/// <para>
/// A null value is valid, so that an optional value can be left unset; pair this with
/// <see cref="RequiredAttribute"/> where a value is mandatory. An empty collection is likewise
/// valid here -- this attribute only checks membership -- so pair it with
/// <see cref="NotEmptyAttribute"/> where a collection must have at least one element.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SupportedArchitectureAttribute : ValidationAttribute
{
    /// <summary>
    /// Whether <see cref="Architectures.Any"/> is an allowed value, alongside every member of
    /// <see cref="PacmanRepositoryValidationConstants.SupportedArchitectures"/>. Defaults to
    /// <c>false</c>: a repository never supports <c>any</c>, only a package build does.
    /// </summary>
    public bool AllowAny { get; init; }

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
                foreach (var element in collection)
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

    private bool IsSupported(string architecture) =>
        PacmanRepositoryValidationConstants.SupportedArchitectures.Contains(architecture, StringComparer.Ordinal)
        || (AllowAny && architecture == Architectures.Any);

    private string Unsupported(string? architecture)
    {
        var allowed = AllowAny
            ? PacmanRepositoryValidationConstants.SupportedArchitectures.Append(Architectures.Any)
            : PacmanRepositoryValidationConstants.SupportedArchitectures;

        return $"'{architecture}' is not a supported architecture. A repository may support: "
            + string.Join(", ", allowed) + ".";
    }
}

using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace PacmanManager.RepoHost.Validation;

/// <summary>
/// Requires a collection-typed value -- anything enumerable, including a dictionary -- to have at
/// least one element.
/// </summary>
/// <remarks>
/// A null value is valid, so that an optional value can be left unset; pair this with
/// <see cref="RequiredAttribute"/> where a value is mandatory.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotEmptyAttribute : ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        switch (value)
        {
            case null:
                return ValidationResult.Success;

            case IEnumerable collection:
            {
                if (collection.Cast<object>().Any())
                {
                    return ValidationResult.Success;
                }

                var memberNames = validationContext.MemberName is { } member ? new[] { member } : null;
                return new ValidationResult(
                    ErrorMessage ?? $"{validationContext.DisplayName} must not be empty.",
                    memberNames);
            }

            default:
                throw new InvalidOperationException(
                    $"{nameof(NotEmptyAttribute)} can only be applied to a collection-typed member; "
                    + $"'{validationContext.MemberName}' is {value.GetType()}.");
        }
    }
}

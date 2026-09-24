using System.ComponentModel.DataAnnotations;
using PacmanManager.Entities;
using PTrampert.QueryObjects.Attributes;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Caller-supplied criteria for narrowing a user listing.
/// </summary>
/// <remarks>
/// The listing is anonymous, so nothing here may reach a user's email: there is no
/// <c>emailContains</c>, which would let anyone ask, one guess at a time, whether a person has an
/// account here.
/// </remarks>
public record UserFilter
{
    /// <summary>
    /// Matches users whose display name contains this value, ignoring case.
    /// </summary>
    /// <remarks>
    /// The term is lowered with <see cref="string.ToLowerInvariant"/> once, as it is bound, and
    /// matched against <see cref="User.NormalizedDisplayName"/>, which is stored lowered the same
    /// way. The match is therefore case-insensitive with no lowering in the query.
    /// </remarks>
    [MaxLength(UserValidationConstants.DisplayNameMaxLength)]
    [StringContainsQuery(nameof(User.NormalizedDisplayName))]
    public string? DisplayNameContains
    {
        get;
        init => field = value?.ToLowerInvariant();
    }
}

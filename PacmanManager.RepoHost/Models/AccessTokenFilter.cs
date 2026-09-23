using System.ComponentModel.DataAnnotations;
using PacmanManager.Entities;
using PTrampert.QueryObjects.Attributes;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Caller-supplied criteria for narrowing an access token listing.
/// </summary>
/// <remarks>
/// The listing is already restricted to the caller's own tokens before this is applied, and every
/// member here can only remove rows from it.
/// </remarks>
public record AccessTokenFilter
{
    /// <summary>
    /// Matches tokens whose name contains this value, ignoring case.
    /// </summary>
    /// <remarks>
    /// The term is lowered with <see cref="string.ToLowerInvariant"/> once, as it is bound, and
    /// matched against <see cref="PacmanAccessToken.NormalizedName"/>, which is stored lowered the
    /// same way. The match is therefore case-insensitive with no lowering in the query.
    /// </remarks>
    [MaxLength(AccessTokenValidationConstants.NameMaxLength)]
    [StringContainsQuery(nameof(PacmanAccessToken.NormalizedName))]
    public string? NameContains
    {
        get;
        init => field = value?.ToLowerInvariant();
    }
}

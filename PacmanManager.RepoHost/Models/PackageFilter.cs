using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.ModelBinding;
using PTrampert.QueryObjects;
using PTrampert.QueryObjects.Attributes;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Caller-supplied criteria for narrowing a package listing.
/// </summary>
/// <remarks>
/// <para>
/// Every member of this record can only ever remove packages from a result set. Visibility is
/// decided separately and applied unconditionally, so no combination of these values can widen what
/// a caller is able to see: naming a repository in <see cref="RepositoryIds"/> that the caller
/// cannot see yields nothing rather than an error.
/// </para>
/// <para>
/// The query attributes say how each criterion narrows the query, and PTrampert.QueryObjects turns
/// them into the predicate. <see cref="Search"/> is the one criterion an attribute cannot express,
/// because it spans three columns, so it is supplied by <see cref="BuildQueryExpression"/> instead
/// and ANDed onto the rest by the same <c>Where</c> call.
/// </para>
/// </remarks>
public record PackageFilter : IQueryObject<PacmanPackage>
{
    /// <summary>
    /// Matches packages in any of these repositories.
    /// </summary>
    /// <remarks>
    /// A repository the caller cannot see contributes nothing, so this narrows the visible set and
    /// never reaches past it.
    /// </remarks>
    [FromCommaSeparatedQuery]
    [AnyOfQuery(nameof(PacmanPackage.RepositoryId))]
    public IEnumerable<Guid>? RepositoryIds { get; init; }

    /// <summary>
    /// Matches packages most recently published by any of these users.
    /// </summary>
    [FromCommaSeparatedQuery]
    [AnyOfQuery(nameof(PacmanPackage.PublisherId))]
    public IEnumerable<Guid>? PublisherIds { get; init; }

    /// <summary>
    /// Matches packages built for this architecture, which is not always their repository's.
    /// </summary>
    [MaxLength(PackageValidationConstants.ArchitectureMaxLength)]
    [EqualsQuery]
    public string? Architecture { get; init; }

    /// <summary>
    /// Matches packages whose name contains this value.
    /// </summary>
    [MaxLength(PackageValidationConstants.NameMaxLength)]
    [StringContainsQuery(nameof(PacmanPackage.Name))]
    public string? NameContains { get; init; }

    /// <summary>
    /// Matches packages published since this instant.
    /// </summary>
    [GreaterThanQuery(nameof(PacmanPackage.UpdatedAt))]
    public DateTimeOffset? UpdatedSince { get; init; }

    /// <summary>
    /// Matches packages whose name, package base or description contains this value, ignoring case.
    /// </summary>
    [MaxLength(PackageValidationConstants.DescriptionMaxLength)]
    public string? Search { get; init; }

    /// <summary>
    /// Builds the part of the filter the query attributes cannot express — today, only
    /// <see cref="Search"/>.
    /// </summary>
    /// <returns>
    /// The predicate for <see cref="Search"/>, or <see langword="null"/> when no search term was
    /// supplied, which contributes nothing to the query.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Two details of the expression below are load-bearing. <c>string.Contains</c> is
    /// case-sensitive under both Npgsql and the in-memory provider, so both sides are lowered
    /// rather than only the term; lowering only the term would quietly stop matching a package
    /// whose name or description is capitalised differently.
    /// </para>
    /// <para>
    /// And <see cref="PacmanPackage.Base"/> and <see cref="PacmanPackage.Description"/> are
    /// nullable, so each is guarded rather than dereferenced. Both providers happen to cope with an
    /// unguarded dereference today — Npgsql emits SQL that simply yields no match, and the in-memory
    /// provider rewrites the member access to be null-safe — but that is a property of the
    /// providers, not of the expression, and it is not something this predicate should be relying
    /// on. Guarded, it means the same thing wherever it is evaluated, including in memory.
    /// </para>
    /// <para>
    /// The <c>lower(…)</c> calls mean no index can serve a search. Nothing indexes these columns
    /// today; moving to Postgres full text search later is a change to this method's body and
    /// nothing else, since no caller, signature or query parameter would move with it.
    /// </para>
    /// </remarks>
    public Expression<Func<PacmanPackage, bool>>? BuildQueryExpression()
    {
        if (string.IsNullOrWhiteSpace(Search))
        {
            return null;
        }

        var term = Search.ToLowerInvariant();
        return package =>
            package.Name.ToLower().Contains(term) ||
            (package.Base != null && package.Base.ToLower().Contains(term)) ||
            (package.Description != null && package.Description.ToLower().Contains(term));
    }
}

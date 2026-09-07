namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Declares which way round a sort field runs when the caller does not say.
/// </summary>
/// <param name="direction">The direction to order by this field in when none was supplied.</param>
/// <remarks>
/// <para>
/// Applied to the members of an enum used as the <c>TSortFields</c> of
/// <see cref="SortOptions{TSortFields}"/>. Which way round is "natural" belongs to the field —
/// newest first for a date, but A→Z for a name — so the answer is declared on the field rather
/// than on the listing, and every listing that offers the field gets the same answer.
/// </para>
/// <para>
/// A field without this attribute takes <see cref="SortFieldDefaults{TSortFields}.Fallback"/>,
/// which keeps the descending default the listings had before per-field defaults existed.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Field)]
public sealed class DefaultSortDirectionAttribute(SortDirection direction) : Attribute
{
    /// <summary>
    /// The direction to order by this field in when the caller supplied none.
    /// </summary>
    public SortDirection Direction { get; } = direction;
}

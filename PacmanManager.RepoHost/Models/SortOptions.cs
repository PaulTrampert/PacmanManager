using System.ComponentModel;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Caller-supplied ordering for a listing, over the properties named by
/// <typeparamref name="TSortFields"/>.
/// </summary>
/// <typeparam name="TSortFields">
/// An enum naming the properties the listing may be ordered by. Its first member is the default
/// ordering, since an omitted query string parameter binds to the enum's zero value.
/// </typeparam>
/// <remarks>
/// <para>
/// Ordering is kept apart from the filter because the two answer different questions: a filter
/// decides which records are in the result set, this decides only the sequence they come back in.
/// Nothing here can change which records a caller sees.
/// </para>
/// <para>
/// What to order by and which way round are separate choices, so they are separate properties.
/// Folding them together would mean a new member for every combination each time a sortable
/// property is added.
/// </para>
/// <para>
/// The type is generic so that every listing spells ordering the same way. Only the set of
/// sortable properties differs between them, so only that is a type parameter; <c>sortBy</c> and
/// <c>direction</c> then mean the same thing on every endpoint by construction rather than by
/// convention.
/// </para>
/// </remarks>
public record SortOptions<TSortFields> where TSortFields : struct, Enum
{
    /// <summary>
    /// The property to order by. Defaults to the first member of <typeparamref name="TSortFields"/>.
    /// </summary>
    public TSortFields SortBy { get; init; }

    /// <summary>
    /// The direction to order in.
    /// </summary>
    [DefaultValue(SortDirection.Descending)]
    public SortDirection Direction { get; init; } = SortDirection.Descending;
}

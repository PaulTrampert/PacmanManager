namespace PacmanManager.RepoHost.Models;

/// <summary>
/// One page of a listing.
/// </summary>
/// <typeparam name="T">The type of the listed items.</typeparam>
/// <typeparam name="TSortField">The sort-field enum of the listing.</typeparam>
public record PaginatedResponse<T, TSortField> where TSortField : struct, Enum
{
    /// <summary>
    /// The items on this page.
    /// </summary>
    public IEnumerable<T> Results { get; init; }

    /// <summary>
    /// The position of the first item on this page within the whole listing.
    /// </summary>
    public int Offset { get; init; }

    /// <summary>
    /// The number of items in the whole listing, across every page.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// The ordering the listing was actually given, with the field's default direction resolved
    /// when the caller supplied none.
    /// </summary>
    public required AppliedSort<TSortField> Sort { get; init; }
}

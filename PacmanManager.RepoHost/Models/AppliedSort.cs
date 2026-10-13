namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The ordering a listing actually ran in, echoed on each page of it.
/// </summary>
/// <remarks>
/// Unlike <see cref="SortOptions{TSortFields}"/>, which is what the caller asked for, nothing here is
/// left to a default: the direction is resolved from the field when the caller omitted it, so a
/// client can show which way round the list runs without knowing each field's default itself.
/// </remarks>
public record AppliedSort
{
    /// <summary>
    /// The sort field the listing was ordered by, as the name of a member of that listing's
    /// sort-field enum.
    /// </summary>
    public required string SortBy { get; init; }

    /// <summary>
    /// The direction the listing was ordered in.
    /// </summary>
    public required SortDirection Direction { get; init; }
}

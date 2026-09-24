namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The properties a user listing may be ordered by.
/// </summary>
/// <remarks>
/// <see cref="SortOptions{TSortFields}"/> takes the first member as its default, so a listing nobody
/// has ordered comes back alphabetical by display name. There is no <c>Created</c> member, because a
/// user has no creation timestamp to back one; a later sort field is an added member here rather than
/// a new query parameter.
/// </remarks>
public enum UserSortField
{
    /// <summary>The user's display name, ignoring case.</summary>
    [DefaultSortDirection(SortDirection.Ascending)]
    DisplayName,
}

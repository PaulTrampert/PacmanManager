using System.ComponentModel;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Caller-supplied ordering for a repository listing.
/// </summary>
/// <remarks>
/// Ordering is kept apart from <see cref="RepositoryFilter"/> because the two answer different
/// questions: a filter decides which repositories are in the result set, this decides only the
/// sequence they come back in. Nothing here can change which repositories a caller sees.
/// </remarks>
public record RepositorySort
{
    /// <summary>
    /// The order to return results in.
    /// </summary>
    [DefaultValue(RepositorySortOrder.CreatedDesc)]
    public RepositorySortOrder Order { get; init; } = RepositorySortOrder.CreatedDesc;
}

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The direction a listing is ordered in.
/// </summary>
public enum SortDirection
{
    /// <summary>Smallest first: earliest date, or alphabetical by name.</summary>
    Ascending,

    /// <summary>Largest first: latest date, or reverse alphabetical by name.</summary>
    Descending,
}

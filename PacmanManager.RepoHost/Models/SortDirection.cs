using System.Text.Json.Serialization;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The direction a listing is ordered in.
/// </summary>
/// <remarks>
/// Serialized by name by its own converter rather than only by the API's global one, so that a
/// client deserializing a <see cref="PaginatedResponse{T}"/> with default options — the E2E tests
/// do — reads the echoed <see cref="AppliedSort.Direction"/> the way the API wrote it.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<SortDirection>))]
public enum SortDirection
{
    /// <summary>Smallest first: earliest date, or alphabetical by name.</summary>
    Ascending,

    /// <summary>Largest first: latest date, or reverse alphabetical by name.</summary>
    Descending,
}

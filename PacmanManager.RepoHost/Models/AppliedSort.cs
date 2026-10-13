using System.Text.Json.Serialization;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The ordering a listing actually ran in, echoed on each page of it.
/// </summary>
/// <typeparam name="TSortField">The sort-field enum of the listing.</typeparam>
/// <remarks>
/// Unlike <see cref="SortOptions{TSortFields}"/>, which is what the caller asked for, nothing here is
/// left to a default: the direction is resolved from the field when the caller omitted it, so a
/// client can show which way round the list runs without knowing each field's default itself.
/// </remarks>
public record AppliedSort<TSortField> where TSortField : struct, Enum
{
    /// <summary>
    /// The sort field the listing was ordered by.
    /// </summary>
    /// <remarks>
    /// Written by name by its own converter rather than only by the API's global one, so that a client
    /// deserializing a page with default options — the E2E tests do — reads it the way the API wrote it.
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required TSortField SortBy { get; init; }

    /// <summary>
    /// The direction the listing was ordered in.
    /// </summary>
    public required SortDirection Direction { get; init; }
}

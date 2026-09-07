using System.Reflection;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// The default sort direction of every member of <typeparamref name="TSortFields"/>, read from the
/// <see cref="DefaultSortDirectionAttribute"/> each member carries.
/// </summary>
/// <typeparam name="TSortFields">An enum naming the properties a listing may be ordered by.</typeparam>
/// <remarks>
/// This is the single place the per-field defaults are resolved, so the ordering a listing applies
/// and the description Swagger publishes for <c>direction</c> cannot drift apart. The lookup is
/// built once per closed generic type, since static state on a generic type is per type argument.
/// </remarks>
public static class SortFieldDefaults<TSortFields> where TSortFields : struct, Enum
{
    /// <summary>
    /// The direction used for a field that carries no <see cref="DefaultSortDirectionAttribute"/>.
    /// </summary>
    public const SortDirection Fallback = SortDirection.Descending;

    private static readonly (TSortFields Field, SortDirection Direction)[] Declared = typeof(TSortFields)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (
            Field: (TSortFields)field.GetValue(null)!,
            Direction: field.GetCustomAttribute<DefaultSortDirectionAttribute>()?.Direction ?? Fallback))
        .ToArray();

    // Grouped rather than keyed directly, because an enum may declare two names for one value and
    // a duplicate key would throw while the type initializer runs.
    private static readonly Dictionary<TSortFields, SortDirection> ByField = Declared
        .GroupBy(entry => entry.Field)
        .ToDictionary(group => group.Key, group => group.First().Direction);

    /// <summary>
    /// The direction <paramref name="field"/> is ordered in when the caller supplied none.
    /// </summary>
    /// <param name="field">The sort field to resolve the default for.</param>
    /// <returns>
    /// The direction declared for the field, or <see cref="Fallback"/> when it declares none — as
    /// an out-of-range value bound from the query string does.
    /// </returns>
    public static SortDirection For(TSortFields field) =>
        ByField.TryGetValue(field, out var direction) ? direction : Fallback;

    /// <summary>
    /// A one-sentence account of every field's default, for the <c>direction</c> parameter's API
    /// documentation.
    /// </summary>
    /// <returns>
    /// A sentence of the form <c>When omitted, defaults to Ascending for Name; Descending for
    /// Created, Updated.</c>
    /// </returns>
    /// <remarks>
    /// Needed because <see cref="SortOptions{TSortFields}.Direction"/> is nullable, so a
    /// <see cref="System.ComponentModel.DefaultValueAttribute"/> can no longer state the default —
    /// and one attribute could not have stated a per-field default anyway.
    /// </remarks>
    public static string Describe()
    {
        var clauses = Declared
            .GroupBy(entry => entry.Direction)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key} for {string.Join(", ", group.Select(entry => entry.Field))}");

        return $"When omitted, defaults to {string.Join("; ", clauses)}.";
    }
}

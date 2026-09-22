namespace PacmanManager.RepoHost.Authentication;

/// <summary>
/// The values of the OAuth 2.0 <c>scope</c> claim that belong to this API, each of the form
/// <c>&lt;audience&gt;:&lt;entity&gt;:&lt;action&gt;</c>.
/// </summary>
/// <remarks>
/// This is the vocabulary only. Nothing here parses a claim or enforces a value; see
/// <c>docs/basic-auth.md</c>, "The <c>scope</c> claim", for the grammar and the rules that govern it.
/// Every value in <see cref="All"/> is registered as a client scope in <c>keycloak/localdev.json</c>.
/// </remarks>
public static class ScopeValues
{
    /// <summary>
    /// The literal prefix on every value, which is also the API's audience.
    /// </summary>
    public const string Audience = "pacman-manager";

    /// <summary>
    /// The wildcard, accepted in place of an entity or an action.
    /// </summary>
    public const string Wildcard = "*";

    /// <summary>
    /// The value permitting every operation on every entity, issued by default to the interactive
    /// clients.
    /// </summary>
    public const string Everything = $"{Audience}:{Wildcard}:{Wildcard}";

    /// <summary>
    /// The entities a value may name, as the plural route segment that serves each, mapped to the
    /// actions that exist for that entity, in order. Packages are upserted, so they have no
    /// <c>update</c>; users are neither created nor deleted by a request; and a token is never
    /// changed once minted.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Entities { get; } =
        new OrderedDictionary<string, IReadOnlyList<string>>
        {
            ["repositories"] = ["read", "create", "update", "delete"],
            ["packages"] = ["read", "create", "delete"],
            ["users"] = ["read", "update"],
            ["tokens"] = ["read", "create", "delete"],
        };

    /// <summary>
    /// The actions a value may name: the union of the actions in <see cref="Entities"/>.
    /// </summary>
    public static IReadOnlyList<string> Actions { get; } =
        Entities.Values.SelectMany(actions => actions).Distinct().ToList();

    /// <summary>
    /// Every value the grammar allows: <see cref="Everything"/> first, then each entity paired with
    /// each action it has, then each <c>&lt;entity&gt;:*</c>, then each <c>*:&lt;action&gt;</c>.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Everything,
        ..Entities.SelectMany(entity => entity.Value.Select(action => For(entity.Key, action))),
        ..Entities.Keys.Select(entity => For(entity, Wildcard)),
        ..Actions.Select(action => For(Wildcard, action)),
    ];

    /// <summary>
    /// Composes the value naming an action on an entity.
    /// </summary>
    /// <param name="entity">A key of <see cref="Entities"/>, or <see cref="Wildcard"/>.</param>
    /// <param name="action">An action <paramref name="entity"/> has, or <see cref="Wildcard"/>.</param>
    /// <returns>The value, prefixed with <see cref="Audience"/>.</returns>
    public static string For(string entity, string action) => $"{Audience}:{entity}:{action}";
}

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
    /// The entities a value may name, as the plural route segment that serves each.
    /// </summary>
    public static IReadOnlyList<string> Entities { get; } = ["repositories", "packages", "users", "tokens"];

    /// <summary>
    /// The actions a value may name.
    /// </summary>
    public static IReadOnlyList<string> Actions { get; } = ["read", "create", "write", "delete", "publish"];

    /// <summary>
    /// Every value the grammar allows: <see cref="Everything"/> first, then each entity and action
    /// pair, then each <c>&lt;entity&gt;:*</c>, then each <c>*:&lt;action&gt;</c>.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Everything,
        ..Entities.SelectMany(entity => Actions.Select(action => For(entity, action))),
        ..Entities.Select(entity => For(entity, Wildcard)),
        ..Actions.Select(action => For(Wildcard, action)),
    ];

    /// <summary>
    /// Composes the value naming an action on an entity.
    /// </summary>
    /// <param name="entity">An entry of <see cref="Entities"/>, or <see cref="Wildcard"/>.</param>
    /// <param name="action">An entry of <see cref="Actions"/>, or <see cref="Wildcard"/>.</param>
    /// <returns>The value, prefixed with <see cref="Audience"/>.</returns>
    public static string For(string entity, string action) => $"{Audience}:{entity}:{action}";
}

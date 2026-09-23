namespace PacmanManager.RepoHost.Authentication;

/// <summary>
/// The values of the OAuth 2.0 <c>scope</c> claim that belong to this API, each of the form
/// <c>&lt;audience&gt;:&lt;entity&gt;:&lt;action&gt;</c>.
/// </summary>
/// <remarks>
/// This is the vocabulary only. <see cref="ActorScope.Parse"/> reads a claim against it, and the
/// access policies enforce what that produces; see
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
    /// The names of the entities a value may name, so that code asking about one entity need not
    /// spell it. <see cref="Entities"/> is the list of them, and says which actions each has.
    /// </summary>
    public static class EntityNames
    {
        /// <summary>Package repositories, served under <c>/repositories</c>.</summary>
        public const string Repositories = "repositories";

        /// <summary>The packages in a repository, served under <c>/packages</c>.</summary>
        public const string Packages = "packages";

        /// <summary>User accounts, served under <c>/users</c>.</summary>
        public const string Users = "users";

        /// <summary>Access tokens, served under <c>/users/me/tokens</c>.</summary>
        public const string Tokens = "tokens";
    }

    /// <summary>
    /// The names of the actions a value may name. Not every entity has every action; see
    /// <see cref="Entities"/>.
    /// </summary>
    public static class ActionNames
    {
        /// <summary>Reading an entity.</summary>
        public const string Read = "read";

        /// <summary>Creating an entity, or for a package, publishing one over an older version.</summary>
        public const string Create = "create";

        /// <summary>Changing an existing entity.</summary>
        public const string Update = "update";

        /// <summary>Deleting an entity.</summary>
        public const string Delete = "delete";
    }

    /// <summary>
    /// The entities a value may name, as the plural route segment that serves each, mapped to the
    /// actions that exist for that entity, in order. Packages are upserted, so they have no
    /// <c>update</c>; users are neither created nor deleted by a request; and a token is never
    /// changed once minted.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Entities { get; } =
        new OrderedDictionary<string, IReadOnlyList<string>>
        {
            [EntityNames.Repositories] = [ActionNames.Read, ActionNames.Create, ActionNames.Update, ActionNames.Delete],
            [EntityNames.Packages] = [ActionNames.Read, ActionNames.Create, ActionNames.Delete],
            [EntityNames.Users] = [ActionNames.Read, ActionNames.Update],
            [EntityNames.Tokens] = [ActionNames.Read, ActionNames.Create, ActionNames.Delete],
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

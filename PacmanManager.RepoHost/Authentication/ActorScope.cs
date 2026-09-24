using System.Collections.Immutable;

namespace PacmanManager.RepoHost.Authentication;

/// <summary>
/// What an <see cref="Actor"/>'s credential may be used for: the values of its <c>scope</c> claim that
/// belong to this API, parsed.
/// </summary>
/// <remarks>
/// <para>
/// A scope only ever narrows. It is a ceiling on what the actor's user may do, never a grant: every
/// ownership and visibility rule still runs after it. Values are <c>OR</c>'d, so an operation is
/// permitted when any one of them names it, and absent means denied, so <see cref="Empty"/> permits
/// nothing. See <c>docs/basic-auth.md</c>, "The <c>scope</c> claim".
/// </para>
/// <para>
/// Two scopes are equal when they permit exactly the same values, whatever order the claim listed
/// them in.
/// </para>
/// </remarks>
public sealed class ActorScope : IEquatable<ActorScope>
{
    private readonly ImmutableHashSet<(string Entity, string Action)> _values;

    private ActorScope(ImmutableHashSet<(string Entity, string Action)> values)
    {
        _values = values;
    }

    /// <summary>
    /// The scope that permits nothing: that of a credential carrying none of our values, and of
    /// <see cref="Actor.Anonymous"/>.
    /// </summary>
    public static ActorScope Empty { get; } = new([]);

    /// <summary>
    /// The scope that permits everything its user may do, as <see cref="ScopeValues.Everything"/>
    /// does. Carried by actors with no credential behind them, such as <see cref="Actor.System"/> and
    /// those a <c>FixedActorAccessor</c> builds.
    /// </summary>
    public static ActorScope Unrestricted { get; } =
        new([(ScopeValues.Wildcard, ScopeValues.Wildcard)]);

    /// <summary>
    /// The values this scope carries, each in its <c>&lt;audience&gt;:&lt;entity&gt;:&lt;action&gt;</c>
    /// form, in no particular order.
    /// </summary>
    public IEnumerable<string> Values => _values.Select(v => ScopeValues.For(v.Entity, v.Action));

    /// <summary>
    /// Whether this scope permits anything but reading. An actor whose scope does not is read-only.
    /// </summary>
    /// <remarks>
    /// Every entity has an action besides <c>read</c>, so a wildcard action always counts.
    /// </remarks>
    public bool PermitsAnyActionButRead =>
        _values.Any(v => v.Action != ScopeValues.ActionNames.Read);

    /// <summary>
    /// Whether this scope permits <paramref name="action"/> on <paramref name="entity"/>.
    /// </summary>
    /// <param name="entity">One of <see cref="ScopeValues.EntityNames"/>.</param>
    /// <param name="action">One of <see cref="ScopeValues.ActionNames"/>.</param>
    /// <returns>True when any value names the entity, or the wildcard, with the action, or the wildcard.</returns>
    public bool Permits(string entity, string action) =>
        _values.Any(v =>
            (v.Entity == ScopeValues.Wildcard || v.Entity == entity)
            && (v.Action == ScopeValues.Wildcard || v.Action == action));

    /// <summary>
    /// Parses an OAuth 2.0 <c>scope</c> claim, keeping the values that belong to this API.
    /// </summary>
    /// <param name="claim">
    /// The claim: one space-delimited, case-sensitive string. <c>null</c> is read as a claim with no
    /// values.
    /// </param>
    /// <param name="logger">Receives a warning for each malformed value of ours that is dropped.</param>
    /// <returns>
    /// The scope the claim's values of ours describe, or <see cref="Empty"/> when it carries none.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A value is ours when it starts with <see cref="ScopeValues.Audience"/> followed by a colon.
    /// Anything else, the bare audience value included, is somebody else's and is ignored silently:
    /// <c>openid</c>, <c>profile</c> and the like are expected.
    /// </para>
    /// <para>
    /// A value of ours is kept only when it has exactly an entity and an action, the entity is one of
    /// <see cref="ScopeValues.Entities"/> or the wildcard, and the action is one that entity has (one
    /// of <see cref="ScopeValues.Actions"/> for the wildcard entity) or the wildcard. Otherwise it is
    /// malformed, and dropped with a warning. Nothing here ever throws on a value: a provider that
    /// mangles one can only take a permission away.
    /// </para>
    /// </remarks>
    public static ActorScope Parse(string? claim, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(claim))
        {
            return Empty;
        }

        var values = ImmutableHashSet.CreateBuilder<(string Entity, string Action)>();
        foreach (var value in claim.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!value.StartsWith($"{ScopeValues.Audience}:", StringComparison.Ordinal))
            {
                continue;
            }

            if (TryParseValue(value) is { } parsed)
            {
                values.Add(parsed);
            }
            else
            {
                logger.LogWarning("Ignoring malformed scope value '{ScopeValue}'", value);
            }
        }

        return values.Count == 0 ? Empty : new ActorScope(values.ToImmutable());
    }

    private static (string Entity, string Action)? TryParseValue(string value)
    {
        var parts = value.Split(':');
        if (parts.Length != 3)
        {
            return null;
        }

        var (entity, action) = (parts[1], parts[2]);
        IEnumerable<string>? actions = entity == ScopeValues.Wildcard
            ? ScopeValues.Actions
            : ScopeValues.Entities.GetValueOrDefault(entity);
        if (actions is null)
        {
            return null;
        }

        return action == ScopeValues.Wildcard || actions.Contains(action) ? (entity, action) : null;
    }

    /// <inheritdoc />
    public bool Equals(ActorScope? other) =>
        other is not null && (ReferenceEquals(this, other) || _values.SetEquals(other._values));

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ActorScope);

    /// <inheritdoc />
    public override int GetHashCode() =>
        _values.Aggregate(0, (hash, value) => hash ^ value.GetHashCode());

    /// <summary>
    /// The scope's values as a claim would carry them: space-delimited, in ordinal order.
    /// </summary>
    /// <returns>The values, or an empty string for <see cref="Empty"/>.</returns>
    public override string ToString() => string.Join(' ', Values.Order(StringComparer.Ordinal));
}

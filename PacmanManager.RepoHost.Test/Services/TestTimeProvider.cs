namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// A <see cref="TimeProvider"/> whose time a test sets, and moves, between arrange and act.
/// </summary>
internal sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <summary>
    /// The time to report.
    /// </summary>
    public DateTimeOffset Now { get; set; } = now;

    /// <summary>
    /// Moves the clock forward.
    /// </summary>
    /// <param name="by">How far.</param>
    public void Advance(TimeSpan by) => Now += by;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => Now;
}

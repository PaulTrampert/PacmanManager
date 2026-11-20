namespace PacmanManager.Worker;

/// <summary>
/// Thrown at startup when a setting the Worker cannot run without is not configured.
/// </summary>
/// <param name="key">The configuration key that has no value, such as <c>ConnectionStrings:rabbitmq</c>.</param>
public class MissingConfigurationException(string key)
    : Exception($"{key} is not configured.")
{
    /// <summary>
    /// The configuration key that has no value.
    /// </summary>
    public string Key { get; } = key;
}

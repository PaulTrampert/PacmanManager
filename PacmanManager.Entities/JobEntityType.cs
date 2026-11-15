namespace PacmanManager.Entities;

/// <summary>
/// The kind of entity a <see cref="JobAffectedEntity"/> names.
/// </summary>
public enum JobEntityType
{
    /// <summary>
    /// A <see cref="PacmanRepository"/>.
    /// </summary>
    Repository,

    /// <summary>
    /// A <see cref="PacmanPackage"/>.
    /// </summary>
    Package,
}

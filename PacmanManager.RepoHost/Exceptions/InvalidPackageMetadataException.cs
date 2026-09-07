namespace PacmanManager.RepoHost.Exceptions;

/// <summary>
/// Thrown when the metadata libalpm reported for an uploaded package cannot be used to name a file
/// on disk — because a value does not match the shape pacman defines for it, because it is too
/// long, or because the name it would produce is not a plain basename.
/// </summary>
/// <remarks>
/// The metadata comes out of a file the caller chose, so it is validated before it is formatted
/// into a path rather than trusted because libalpm reported it.
/// </remarks>
/// <param name="field">The metadata field that was rejected.</param>
/// <param name="reason">Why it was rejected.</param>
public class InvalidPackageMetadataException(string field, string reason)
    : InvalidPackageException($"Package metadata field '{field}' is not usable: {reason}")
{
    /// <summary>
    /// The metadata field that was rejected.
    /// </summary>
    public string Field { get; } = field;
}

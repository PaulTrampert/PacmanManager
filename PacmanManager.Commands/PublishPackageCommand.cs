namespace PacmanManager.Commands;

/// <summary>
/// Asks for an uploaded package file to be published into a repository.
/// </summary>
/// <remarks>
/// A plain data object: it carries only what the publish needs. Tracking state, the user who
/// scheduled the publish and the entities it affects belong to the job whose message it is.
/// </remarks>
public record PublishPackageCommand
{
    /// <summary>
    /// The id of the repository to publish the package into.
    /// </summary>
    public required Guid RepositoryId { get; init; }

    /// <summary>
    /// The path of the uploaded package file to publish.
    /// </summary>
    public required string PackageFilePath { get; init; }
}

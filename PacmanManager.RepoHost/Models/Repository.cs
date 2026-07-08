using PacmanManager.Entities;

namespace PacmanManager.RepoHost.Models;

/// <summary>
/// Represents a pacman repository that can be hosted and managed.
/// </summary>
public record Repository
{
    /// <summary>
    /// Internal database Id for the repository. Can be used for API calls but
    /// pacman is unaware of this value.
    /// </summary>
    public required Guid Id { get; init; }
    
    /// <summary>
    /// Repository name (e.g., "core", "extra", "custom-repo").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Repository architecture (e.g., "x86_64", "any").
    /// </summary>
    public string Architecture { get; init; } = "x86_64";

    /// <summary>
    /// Whether the repository is public.
    /// </summary>
    public bool IsPublic { get; init; } = false;
    
    /// <summary>
    /// The owner of the repository.
    /// </summary>
    public PublicUserInfo Owner { get; init; }
    
    /// <summary>
    /// When the repository was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
    
    /// <summary>
    /// When the repository was last modified.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
    
    /// <summary>
    /// Gets the file system path for this repository based on its name and architecture.
    /// </summary>
    /// <param name="baseRepositoryPath">The base path where repositories are stored.</param>
    /// <returns>The full path to this repository's directory.</returns>
    public string GetPath(string baseRepositoryPath)
    {
        return Path.Combine(baseRepositoryPath, Name, Architecture);
    }

    /// <summary>
    /// Creates a new repository model from the underlying db object.
    /// </summary>
    /// <param name="repository">Repository to create db object from</param>
    /// <returns>New Repository api model.</returns>
    public static Repository FromPacmanRepository(PacmanRepository repository)
    {
        return new Repository
        {
            Id = repository.Id,
            Name = repository.Name,
            Architecture = repository.Architecture,
            IsPublic = repository.IsPublic,
            Owner = PublicUserInfo.FromUser(repository.Owner),
            CreatedAt = repository.CreatedAt,
            UpdatedAt = repository.UpdatedAt,
        };
    }
}

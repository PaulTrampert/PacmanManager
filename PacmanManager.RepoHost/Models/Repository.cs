using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
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
    /// The machine architectures the repository publishes a database for (e.g., <c>["x86_64"]</c>).
    /// Never <c>any</c>: a package built for <c>any</c> is listed in every one of these.
    /// </summary>
    public IEnumerable<string> SupportedArchitectures { get; init; } = [];

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
    /// What the caller of this request may do to the repository.
    /// </summary>
    /// <remarks>
    /// Computed for the caller after the repository is read, never stored and never part of
    /// <see cref="Projection"/>: the service sets it with a <c>with</c> expression. Until then it is
    /// <see cref="RepositoryPermissions.None"/>, so a repository nobody computed it for permits
    /// nothing. <see cref="RequiredAttribute"/> rather than <c>required</c>, so that the projection
    /// need not name it, while the OpenAPI document still lists it as always present.
    /// </remarks>
    [Required]
    public RepositoryPermissions Permissions { get; init; } = RepositoryPermissions.None;
    
    /// <summary>
    /// The URL a <c>pacman.conf</c> section's <c>Server</c> line names to reach this repository, such
    /// as <c>https://packages.example.com/pacman/$repo/$arch</c>. <c>$repo</c> and <c>$arch</c> are
    /// literal, for pacman to fill in.
    /// </summary>
    /// <remarks>
    /// Built from the <c>PublicUrl</c> setting, or the request's root when it is not set, each time the
    /// repository is returned; it is never stored. Neither <see cref="Projection"/> nor
    /// <see cref="FromPacmanRepository"/> sets it.
    /// </remarks>
    [Required]
    public string ServerUrl { get; init; } = string.Empty;

    /// <summary>
    /// Projects the underlying db object onto this model inside a query.
    /// </summary>
    /// <remarks>
    /// Preferred over <see cref="FromPacmanRepository"/> on read paths: it pulls the owner in the
    /// same round trip, so callers cannot forget to include the navigation and hit a null
    /// reference, and it never fetches columns the model does not expose.
    /// </remarks>
    public static Expression<Func<PacmanRepository, Repository>> Projection => repository => new Repository
    {
        Id = repository.Id,
        Name = repository.Name,
        SupportedArchitectures = repository.SupportedArchitectures,
        IsPublic = repository.IsPublic,
        Owner = new PublicUserInfo
        {
            Id = repository.Owner.Id,
            DisplayName = repository.Owner.DisplayName,
        },
        CreatedAt = repository.CreatedAt,
        UpdatedAt = repository.UpdatedAt,
    };

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
            SupportedArchitectures = repository.SupportedArchitectures,
            IsPublic = repository.IsPublic,
            Owner = PublicUserInfo.FromUser(repository.Owner),
            CreatedAt = repository.CreatedAt,
            UpdatedAt = repository.UpdatedAt,
        };
    }
}

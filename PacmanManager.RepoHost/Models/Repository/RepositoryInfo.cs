namespace PacmanManager.RepoHost.Models.Repository;

public record RepositoryInfo
{
    public required string Name { get; init; }
    public required string Url { get; init; }
};
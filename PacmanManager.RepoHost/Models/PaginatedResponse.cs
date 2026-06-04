namespace PacmanManager.RepoHost.Models;

public record PaginatedResponse<T>
{
    public IEnumerable<T> Results { get; init; }
    public int Offset { get; init; }
    public int Total { get; init; }
}
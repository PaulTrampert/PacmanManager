using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PacmanManager.RepoHost.Models;

public record PaginationParams
{
    [Range(0, int.MaxValue)]
    public int Offset { get; init; }
    
    [DefaultValue(50)]
    [Range(1, 500)]
    public int PageSize { get; init; } = 50;
}
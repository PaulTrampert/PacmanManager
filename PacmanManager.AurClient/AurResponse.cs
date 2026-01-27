using System.Text.Json.Serialization;

namespace PacmanManager.AurClient;

[JsonConverter(typeof(AurResponseConverter))]
public record AurResponse
{
    public int Version { get; init; }
    
    public AurReturnType Type { get; init; }
}

public record AurResponse<T> : AurResponse
{
    public int ResultCount { get; init; }
    
    public IEnumerable<T> Results { get; init; }
}
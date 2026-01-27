namespace PacmanManager.AurClient;

public record AurErrorResponse : AurResponse
{
    public string Error { get; init; }
}
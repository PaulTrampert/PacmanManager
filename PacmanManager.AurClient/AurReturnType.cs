using System.Text.Json.Serialization;

namespace PacmanManager.AurClient;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AurReturnType
{
    [JsonPropertyName("info")]
    Info,
    
    [JsonPropertyName("multiinfo")]
    MultiInfo,
    
    [JsonPropertyName("search")]
    Search,
    
    [JsonPropertyName("error")]
    Error,
}
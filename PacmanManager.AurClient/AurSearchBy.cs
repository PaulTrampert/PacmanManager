using System.Text.Json.Serialization;

namespace PacmanManager.AurClient;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AurSearchBy
{
    [JsonStringEnumMemberName("name")]
    Name,
    
    [JsonStringEnumMemberName("name-desc")]
    NameDesc,
    
    [JsonStringEnumMemberName("maintainer")]
    Maintainer,
    
    [JsonStringEnumMemberName("depends")]
    Depends,
    
    [JsonStringEnumMemberName("makedepends")]
    MakeDepends,
    
    [JsonStringEnumMemberName("optdepends")]
    OptDepends,
    
    [JsonStringEnumMemberName("checkdepends")]
    CheckDepends
}
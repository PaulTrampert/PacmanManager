using System.Text.Json;
using System.Text.Json.Serialization;

namespace PacmanManager.AurClient;

public class AurResponseConverter : JsonConverter<AurResponse>
{
    public override AurResponse? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (!document.RootElement.TryGetProperty("type", out var typeProperty) || !Enum.TryParse<AurReturnType>(typeProperty.GetString(), ignoreCase: true, result: out var returnType))
        {
            throw new JsonException("Invalid or missing 'type' property in AUR response.");
        }
        return returnType switch
        {
            AurReturnType.Info => document.RootElement.Deserialize<AurResponse<AurFullPackageInfo>>(options),
            AurReturnType.MultiInfo => document.RootElement.Deserialize<AurResponse<AurFullPackageInfo>>(options),
            AurReturnType.Search => document.RootElement.Deserialize<AurResponse<AurBasicPackageInfo>>(options),
            AurReturnType.Error => document.RootElement.Deserialize<AurErrorResponse>(options),
            _ => throw new JsonException($"Unsupported AUR return type: {returnType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, AurResponse value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
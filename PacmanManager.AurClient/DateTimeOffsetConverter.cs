using System.Text.Json;
using System.Text.Json.Serialization;

namespace PacmanManager.AurClient;

public class NullableDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            var unixTimeSeconds = reader.GetInt64();
            return DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds);
        }

        throw new JsonException("Invalid DateTimeOffset format.");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value.ToUnixTimeSeconds());
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
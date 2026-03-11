using System.Text.Json;
using System.Text.Json.Serialization;
using SreAgent.Framework.Contexts;

namespace SreAgent.Repository.Serialization;

/// <summary>
/// JSON converter for the polymorphic MessagePart hierarchy.
/// Used by both PostgresContextStore and CheckpointService.
/// </summary>
public class MessagePartJsonConverter : JsonConverter<MessagePart>
{
    public override MessagePart? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeProp))
        {
            if (!root.TryGetProperty("Type", out typeProp))
                throw new JsonException("MessagePart missing 'type' discriminator");
        }

        var typeValue = typeProp.GetInt32();
        var partType = (MessagePartType)typeValue;
        var rawText = root.GetRawText();

        return partType switch
        {
            MessagePartType.Text => JsonSerializer.Deserialize<TextPart>(rawText, StripConverterOptions(options)),
            MessagePartType.ToolCall => JsonSerializer.Deserialize<ToolCallPart>(rawText, StripConverterOptions(options)),
            MessagePartType.ToolResult => JsonSerializer.Deserialize<ToolResultPart>(rawText, StripConverterOptions(options)),
            MessagePartType.Image => JsonSerializer.Deserialize<ImagePart>(rawText, StripConverterOptions(options)),
            MessagePartType.Error => JsonSerializer.Deserialize<ErrorPart>(rawText, StripConverterOptions(options)),
            _ => throw new JsonException($"Unknown MessagePartType: {typeValue}")
        };
    }

    public override void Write(Utf8JsonWriter writer, MessagePart value, JsonSerializerOptions options)
    {
        var stripped = StripConverterOptions(options);
        JsonSerializer.Serialize(writer, value, value.GetType(), stripped);
    }

    private static JsonSerializerOptions StripConverterOptions(JsonSerializerOptions options)
    {
        var newOptions = new JsonSerializerOptions(options);
        for (var i = newOptions.Converters.Count - 1; i >= 0; i--)
        {
            if (newOptions.Converters[i] is MessagePartJsonConverter)
                newOptions.Converters.RemoveAt(i);
        }
        return newOptions;
    }

    public static JsonSerializerOptions DefaultOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new MessagePartJsonConverter() }
    };
}

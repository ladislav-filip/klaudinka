using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeAgent.Models;

/// <summary>
/// Konverzační zpráva odesílaná nebo přijímaná přes Anthropic Messages API.
/// Obsah může být prostý řetězec nebo seznam <see cref="ContentBlock"/>.
/// </summary>
[JsonConverter(typeof(MessageJsonConverter))]
public sealed class Message
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Obsah zprávy — buď text uživatele, nebo strukturované bloky (tool_use / tool_result).
    /// </summary>
    public object Content { get; set; } = string.Empty;

    /// <summary>Vytvoří uživatelskou zprávu s textovým obsahem.</summary>
    public static Message UserText(string text) => new()
    {
        Role = "user",
        Content = text
    };

    /// <summary>Vytvoří uživatelskou zprávu s výsledky nástrojů.</summary>
    public static Message UserToolResults(IEnumerable<ContentBlock> results) => new()
    {
        Role = "user",
        Content = results.ToList()
    };

    /// <summary>Vytvoří asistentskou zprávu s bloky obsahu z API odpovědi.</summary>
    public static Message Assistant(IEnumerable<ContentBlock> blocks) => new()
    {
        Role = "assistant",
        Content = blocks.ToList()
    };
}

/// <summary>
/// Vlastní JSON konvertor pro <see cref="Message"/>, protože pole content
/// může být buď string, nebo pole objektů.
/// </summary>
public sealed class MessageJsonConverter : JsonConverter<Message>
{
    public override Message? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Očekáván JSON objekt pro Message.");
        }

        var message = new Message();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return message;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName)
            {
                case "role":
                    message.Role = reader.GetString() ?? string.Empty;
                    break;
                case "content":
                    message.Content = ReadContent(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        throw new JsonException("Neočekávaný konec JSON při čtení Message.");
    }

    public override void Write(Utf8JsonWriter writer, Message value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("role", value.Role);

        writer.WritePropertyName("content");
        switch (value.Content)
        {
            case string text:
                writer.WriteStringValue(text);
                break;
            case List<ContentBlock> blocks:
                JsonSerializer.Serialize(writer, blocks, options);
                break;
            case IEnumerable<ContentBlock> enumerable:
                JsonSerializer.Serialize(writer, enumerable.ToList(), options);
                break;
            default:
                throw new JsonException($"Nepodporovaný typ obsahu zprávy: {value.Content.GetType().Name}");
        }

        writer.WriteEndObject();
    }

    private static object ReadContent(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.StartArray => JsonSerializer.Deserialize<List<ContentBlock>>(ref reader, options)
                ?? new List<ContentBlock>(),
            _ => throw new JsonException($"Neočekávaný token pro Message.content: {reader.TokenType}")
        };
    }
}

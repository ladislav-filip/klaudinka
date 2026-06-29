using System.Text.Json;
using ClaudeAgent.Models;

namespace ClaudeAgent.Tests;

/// <summary>
/// Testy serializace a deserializace konverzačních zpráv.
/// </summary>
public class MessageSerializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new MessageJsonConverter() }
    };

    [Fact]
    public void Serialize_UserTextMessage()
    {
        var message = Message.UserText("Hello");

        var json = JsonSerializer.Serialize(message, Options);

        Assert.Contains("\"role\":\"user\"", json.Replace(" ", ""));
        Assert.Contains("\"content\":\"Hello\"", json.Replace(" ", ""));
    }

    [Fact]
    public void Serialize_UserToolResults()
    {
        var message = Message.UserToolResults([
            ContentBlock.ToolResult("toolu_01", "výsledek")
        ]);

        var json = JsonSerializer.Serialize(message, Options);

        Assert.Contains("tool_result", json);
        Assert.Contains("toolu_01", json);
    }

    [Fact]
    public void RoundTrip_AssistantMessage()
    {
        var original = Message.Assistant([
            ContentBlock.TextBlock("Odpověď"),
            new ContentBlock { Type = "tool_use", Id = "id1", Name = "read_file", Input = JsonDocument.Parse("{}").RootElement }
        ]);

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<Message>(json, Options);

        Assert.NotNull(deserialized);
        Assert.Equal("assistant", deserialized.Role);
        Assert.IsType<List<ContentBlock>>(deserialized.Content);
    }
}

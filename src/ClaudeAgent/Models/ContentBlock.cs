using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeAgent.Models;

/// <summary>
/// Reprezentuje jeden blok obsahu v rámci zprávy Anthropic API.
/// Podporuje typy: text, tool_use a tool_result.
/// </summary>
public sealed class ContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Identifikátor tool_use bloku (pouze pro type == "tool_use").</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Název nástroje (pouze pro type == "tool_use").</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Vstupní parametry nástroje (pouze pro type == "tool_use").</summary>
    [JsonPropertyName("input")]
    public JsonElement? Input { get; set; }

    /// <summary>Výsledek volání nástroje (pouze pro type == "tool_result").</summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>Odkaz na odpovídající tool_use blok (pouze pro type == "tool_result").</summary>
    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; set; }

    /// <summary>Textový obsah (pouze pro type == "text").</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>Vytvoří textový blok.</summary>
    public static ContentBlock TextBlock(string text) => new()
    {
        Type = "text",
        Text = text
    };

    /// <summary>Vytvoří blok výsledku nástroje pro odeslání zpět do API.</summary>
    public static ContentBlock ToolResult(string toolUseId, string content) => new()
    {
        Type = "tool_result",
        ToolUseId = toolUseId,
        Content = content
    };
}

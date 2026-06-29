using System.Text.Json.Serialization;

namespace ClaudeAgent.Models;

/// <summary>
/// Definice nástroje odesílaná do Anthropic API v poli <c>tools</c>.
/// </summary>
public sealed class ToolDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("input_schema")]
    public object InputSchema { get; set; } = new { };
}

/// <summary>
/// Požadavek na Anthropic Messages API.
/// </summary>
public sealed class ApiRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "claude-sonnet-4-6";

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 8096;

    [JsonPropertyName("system")]
    public string System { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<Message> Messages { get; set; } = new();

    [JsonPropertyName("tools")]
    public List<ToolDefinition>? Tools { get; set; }
}

/// <summary>
/// Odpověď z Anthropic Messages API.
/// </summary>
public sealed class ApiResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public List<ContentBlock> Content { get; set; } = new();

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    [JsonPropertyName("usage")]
    public ApiUsage? Usage { get; set; }
}

/// <summary>
/// Statistiky spotřeby tokenů v odpovědi API.
/// </summary>
public sealed class ApiUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

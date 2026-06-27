using System.Text.Json;
using ClaudeAgent.Models;
using ClaudeAgent.Tools;

namespace ClaudeAgent;

/// <summary>
/// Událost vyvolaná při spuštění nástroje — používá se pro výpis průběhu v REPL.
/// </summary>
public sealed class ToolCallEventArgs : EventArgs
{
    public required string ToolName { get; init; }
    public required string InputJson { get; init; }
}

/// <summary>
/// Událost vyvolaná po dokončení nástroje.
/// </summary>
public sealed class ToolCompletedEventArgs : EventArgs
{
    public required string ToolName { get; init; }
    public required bool Success { get; init; }
}

/// <summary>
/// Implementuje agentní smyčku: opakovaně volá API, vykonává tool_use bloky
/// a přidává tool_result zprávy, dokud model neskončí s stop_reason == end_turn.
/// </summary>
public sealed class AgentLoop
{
    private readonly AnthropicClient _client;
    private readonly ToolRegistry _toolRegistry;
    private readonly string _systemPrompt;
    private readonly string _model;

    public AgentLoop(
        AnthropicClient client,
        ToolRegistry toolRegistry,
        string systemPrompt,
        string model = "claude-sonnet-4-6")
    {
        _client = client;
        _toolRegistry = toolRegistry;
        _systemPrompt = systemPrompt;
        _model = model;
    }

    /// <summary>Vyvoláno před spuštěním nástroje.</summary>
    public event EventHandler<ToolCallEventArgs>? ToolCallStarted;

    /// <summary>Vyvoláno po dokončení nástroje.</summary>
    public event EventHandler<ToolCompletedEventArgs>? ToolCallCompleted;

    /// <summary>
    /// Zpracuje uživatelský vstup v kontextu existující konverzace.
    /// Vrací finální textovou odpověď asistenta.
    /// </summary>
    public async Task<string> ProcessUserInputAsync(
        List<Message> conversation,
        string userInput,
        CancellationToken cancellationToken = default)
    {
        conversation.Add(Message.UserText(userInput));

        while (true)
        {
            var request = new ApiRequest
            {
                Model = _model,
                MaxTokens = 8096,
                System = _systemPrompt,
                Messages = conversation,
                Tools = _toolRegistry.GetToolDefinitions()
            };

            var response = await _client.SendAsync(request, cancellationToken);

            conversation.Add(Message.Assistant(response.Content));

            if (response.StopReason == "end_turn")
            {
                return ExtractTextResponse(response.Content);
            }

            if (response.StopReason == "tool_use")
            {
                var toolResults = await ExecuteToolUsesAsync(response.Content, cancellationToken);
                conversation.Add(Message.UserToolResults(toolResults));
                continue;
            }

            // Neočekávaný stop_reason — vrátíme dostupný text nebo informaci o stavu
            var text = ExtractTextResponse(response.Content);
            return string.IsNullOrWhiteSpace(text)
                ? $"Neočekávaný stop_reason: {response.StopReason ?? "null"}"
                : text;
        }
    }

    private async Task<List<ContentBlock>> ExecuteToolUsesAsync(
        List<ContentBlock> assistantContent,
        CancellationToken cancellationToken)
    {
        var results = new List<ContentBlock>();

        foreach (var block in assistantContent.Where(b => b.Type == "tool_use"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var toolName = block.Name ?? "unknown";
            var inputJson = block.Input?.GetRawText() ?? "{}";

            ToolCallStarted?.Invoke(this, new ToolCallEventArgs
            {
                ToolName = toolName,
                InputJson = inputJson
            });

            var tool = _toolRegistry.GetTool(toolName);
            string result;

            if (tool is null)
            {
                result = $"Chyba: nástroj '{toolName}' není registrován.";
            }
            else
            {
                var input = block.Input ?? default;
                result = await tool.ExecuteAsync(input);
            }

            var success = !result.StartsWith("Chyba", StringComparison.OrdinalIgnoreCase);
            ToolCallCompleted?.Invoke(this, new ToolCompletedEventArgs
            {
                ToolName = toolName,
                Success = success
            });

            results.Add(ContentBlock.ToolResult(block.Id ?? string.Empty, result));
        }

        return results;
    }

    /// <summary>Extrahuje text z content bloků odpovědi.</summary>
    internal static string ExtractTextResponse(List<ContentBlock> content) =>
        string.Join("\n", content
            .Where(b => b.Type == "text" && !string.IsNullOrEmpty(b.Text))
            .Select(b => b.Text));
}

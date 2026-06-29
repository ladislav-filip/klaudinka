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
    /// <summary>Výchozí model Anthropic API.</summary>
    public const string DefaultModel = "claude-sonnet-4-6";

    /// <summary>Výchozí maximální počet tokenů v odpovědi.</summary>
    public const int DefaultMaxTokens = 8096;

    /// <summary>Výchozí maximální počet kol volání nástrojů v jednom uživatelském požadavku.</summary>
    public const int DefaultMaxToolIterations = 25;

    private readonly AnthropicClient _client;
    private readonly ToolRegistry _toolRegistry;
    private readonly string _systemPrompt;
    private readonly string _model;
    private readonly int _maxToolIterations;
    private readonly int _maxTokens;

    public AgentLoop(
        AnthropicClient client,
        ToolRegistry toolRegistry,
        string systemPrompt,
        string model = DefaultModel,
        int maxToolIterations = DefaultMaxToolIterations,
        int maxTokens = DefaultMaxTokens)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxToolIterations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTokens);

        _client = client;
        _toolRegistry = toolRegistry;
        _systemPrompt = systemPrompt;
        _model = model;
        _maxToolIterations = maxToolIterations;
        _maxTokens = maxTokens;
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

        var toolIterations = 0;

        while (true)
        {
            var request = new ApiRequest
            {
                Model = _model,
                MaxTokens = _maxTokens,
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
                var toolUseBlocks = response.Content.Where(b => b.Type == "tool_use").ToList();

                // tool_use stop_reason bez jediného tool_use bloku — bez explicitního
                // ošetření bychom odeslali prázdný tool_result a smyčka by se zacyklila.
                if (toolUseBlocks.Count == 0)
                {
                    var partialText = ExtractTextResponse(response.Content);
                    return string.IsNullOrWhiteSpace(partialText)
                        ? "Chyba: model signalizoval tool_use, ale neposlal žádný tool_use blok."
                        : partialText;
                }

                var toolResults = await ExecuteToolUsesAsync(toolUseBlocks, cancellationToken);
                conversation.Add(Message.UserToolResults(toolResults));

                // Limit kontrolujeme až po přidání tool_results, aby konverzace skončila
                // ve validním stavu (user tool_results) a šla případně dál pokračovat.
                if (++toolIterations >= _maxToolIterations)
                {
                    return $"Chyba: dosažen limit {_maxToolIterations} iterací nástrojů. " +
                           "Agentní smyčka byla zastavena, aby se předešlo nekonečnému cyklu.";
                }

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
        List<ContentBlock> toolUseBlocks,
        CancellationToken cancellationToken)
    {
        var results = new List<ContentBlock>();

        foreach (var block in toolUseBlocks)
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

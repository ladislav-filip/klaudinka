using System.Text.Json;

namespace ClaudeAgent.Tools;

/// <summary>
/// Kontrakt pro nástroje dostupné agentní smyčce.
/// Každý nástroj je mapován na Anthropic tool definition a lze ho vykonat lokálně.
/// </summary>
public interface ITool
{
    /// <summary>Identifikátor nástroje používaný v API (např. read_file).</summary>
    string Name { get; }

    /// <summary>Lidsky čitelný popis pro model.</summary>
    string Description { get; }

    /// <summary>JSON Schema vstupních parametrů.</summary>
    object InputSchema { get; }

    /// <summary>Vykoná nástroj s parametry z tool_use bloku.</summary>
    Task<string> ExecuteAsync(JsonElement input);
}

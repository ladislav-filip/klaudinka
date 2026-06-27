using ClaudeAgent.Models;

namespace ClaudeAgent.Tools;

/// <summary>
/// Centrální registr všech dostupných nástrojů.
/// Poskytuje definice pro API a vyhledání nástroje podle jména.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools;

    public ToolRegistry(IEnumerable<ITool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
    }

    /// <summary>Všechna registrovaná jména nástrojů.</summary>
    public IReadOnlyCollection<string> ToolNames => _tools.Keys;

    /// <summary>Převede registrované nástroje na definice pro Anthropic API.</summary>
    public List<ToolDefinition> GetToolDefinitions() =>
        _tools.Values.Select(tool => new ToolDefinition
        {
            Name = tool.Name,
            Description = tool.Description,
            InputSchema = tool.InputSchema
        }).ToList();

    /// <summary>Najde nástroj podle jména; vrací null pokud neexistuje.</summary>
    public ITool? GetTool(string name) =>
        _tools.TryGetValue(name, out var tool) ? tool : null;
}

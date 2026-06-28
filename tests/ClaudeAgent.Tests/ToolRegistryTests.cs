using ClaudeAgent.Tools;

namespace ClaudeAgent.Tests;

/// <summary>
/// Testy registrace a vyhledávání nástrojů.
/// </summary>
public class ToolRegistryTests
{
    [Fact]
    public void GetToolDefinitions_VraciVsechnyNastroje()
    {
        var registry = CreateRegistry();

        var definitions = registry.GetToolDefinitions();

        Assert.Equal(3, definitions.Count);
        Assert.Contains(definitions, d => d.Name == "read_file");
        Assert.Contains(definitions, d => d.Name == "write_file");
        Assert.Contains(definitions, d => d.Name == "list_files");
    }

    [Fact]
    public void GetTool_NajdeExistujiciNastroj()
    {
        var registry = CreateRegistry();

        var tool = registry.GetTool("read_file");

        Assert.NotNull(tool);
        Assert.Equal("read_file", tool.Name);
    }

    [Fact]
    public void GetTool_VraciNullProNeexistujiciNastroj()
    {
        var registry = CreateRegistry();

        Assert.Null(registry.GetTool("neexistuje"));
    }

    private static ToolRegistry CreateRegistry()
    {
        var root = Directory.GetCurrentDirectory();
        return new([
            new ReadFileTool(root),
            new WriteFileTool(root),
            new ListFilesTool(root)
        ]);
    }
}

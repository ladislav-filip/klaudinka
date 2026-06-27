using System.Text.Json;
using ClaudeAgent.Tools;

namespace ClaudeAgent.Tests;

/// <summary>
/// Testy nástroje list_files.
/// </summary>
public class ListFilesToolTests : IDisposable
{
    private readonly string _tempDir;

    public ListFilesToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"claude-agent-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "a");
        File.WriteAllText(Path.Combine(_tempDir, "b.cs"), "b");
    }

    [Fact]
    public async Task ExecuteAsync_VratiJsonSeznamSouboru()
    {
        var tool = new ListFilesTool();
        var input = JsonDocument.Parse($"{{\"path\": \"{_tempDir.Replace("\\", "\\\\")}\"}}").RootElement;

        var result = await tool.ExecuteAsync(input);

        using var doc = JsonDocument.Parse(result);
        var files = doc.RootElement.EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();

        Assert.Contains("a.txt", files);
        Assert.Contains("b.cs", files);
    }

    [Fact]
    public async Task ExecuteAsync_FiltrujePodlePatternu()
    {
        var tool = new ListFilesTool();
        var input = JsonDocument.Parse(
            $"{{\"path\": \"{_tempDir.Replace("\\", "\\\\")}\", \"pattern\": \"*.cs\"}}").RootElement;

        var result = await tool.ExecuteAsync(input);

        using var doc = JsonDocument.Parse(result);
        var files = doc.RootElement.EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();

        Assert.Single(files);
        Assert.Equal("b.cs", files[0]);
    }

    [Fact]
    public async Task ExecuteAsync_VratiChybuProNeexistujiciAdresar()
    {
        var tool = new ListFilesTool();
        var input = JsonDocument.Parse("{\"path\": \"neexistujici-adresar\"}").RootElement;

        var result = await tool.ExecuteAsync(input);

        Assert.StartsWith("Chyba:", result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}

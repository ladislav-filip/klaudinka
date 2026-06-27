using System.Text.Json;
using ClaudeAgent.Tools;

namespace ClaudeAgent.Tests;

/// <summary>
/// Testy nástroje write_file.
/// </summary>
public class WriteFileToolTests : IDisposable
{
    private readonly string _tempDir;

    public WriteFileToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"claude-agent-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task ExecuteAsync_ZapiseSoubor()
    {
        var filePath = Path.Combine(_tempDir, "output.txt");
        var tool = new WriteFileTool();
        var input = JsonDocument.Parse(
            $"{{\"path\": \"{filePath.Replace("\\", "\\\\")}\", \"content\": \"Test obsah\"}}").RootElement;

        var result = await tool.ExecuteAsync(input);

        Assert.Contains("úspěšně", result);
        Assert.Equal("Test obsah", await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task ExecuteAsync_VytvoriNadrazenyAdresar()
    {
        var filePath = Path.Combine(_tempDir, "subdir", "nested.txt");
        var tool = new WriteFileTool();
        var input = JsonDocument.Parse(
            $"{{\"path\": \"{filePath.Replace("\\", "\\\\")}\", \"content\": \"Nested\"}}").RootElement;

        var result = await tool.ExecuteAsync(input);

        Assert.Contains("úspěšně", result);
        Assert.True(File.Exists(filePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}

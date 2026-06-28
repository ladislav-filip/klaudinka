using System.Text.Json;
using ClaudeAgent.Tools;

namespace ClaudeAgent.Tests;

/// <summary>
/// Testy nástroje read_file.
/// </summary>
public class ReadFileToolTests : IDisposable
{
    private readonly string _tempDir;

    public ReadFileToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"claude-agent-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task ExecuteAsync_PrecteTextovySoubor()
    {
        var filePath = Path.Combine(_tempDir, "test.txt");
        await File.WriteAllTextAsync(filePath, "Ahoj světe!");

        var tool = new ReadFileTool(_tempDir);
        var input = JsonDocument.Parse($"{{\"path\": \"{filePath.Replace("\\", "\\\\")}\"}}").RootElement;

        var result = await tool.ExecuteAsync(input);

        Assert.Equal("Ahoj světe!", result);
    }

    [Fact]
    public async Task ExecuteAsync_VratiChybuProNeexistujiciSoubor()
    {
        var tool = new ReadFileTool(_tempDir);
        var input = JsonDocument.Parse("{\"path\": \"neexistuje.txt\"}").RootElement;

        var result = await tool.ExecuteAsync(input);

        Assert.StartsWith("Chyba:", result);
    }

    [Fact]
    public async Task ExecuteAsync_VratiChybuPriChybejicimPath()
    {
        var tool = new ReadFileTool(_tempDir);
        var input = JsonDocument.Parse("{}").RootElement;

        var result = await tool.ExecuteAsync(input);

        Assert.Contains("path", result);
    }

    [Fact]
    public void IsTextFile_VraciFalseProBinarniSoubor()
    {
        var filePath = Path.Combine(_tempDir, "binary.bin");
        File.WriteAllBytes(filePath, [0x00, 0x01, 0x02]);

        Assert.False(ReadFileTool.IsTextFile(filePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}

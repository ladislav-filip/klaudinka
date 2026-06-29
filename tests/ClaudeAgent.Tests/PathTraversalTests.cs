using System.Text.Json;
using ClaudeAgent.Tools;

namespace ClaudeAgent.Tests;

/// <summary>
/// Integrační testy sandboxingu cest přes file tooly (nález C1 — path traversal).
/// Ověřuje, že read_file / write_file / list_files odmítnou přístup mimo workspace.
/// </summary>
public class PathTraversalTests : IDisposable
{
    private readonly string _workspace;
    private readonly string _outsideDir;
    private readonly string _secretFile;

    public PathTraversalTests()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"claude-agent-test-{Guid.NewGuid():N}");
        _workspace = Path.Combine(baseDir, "workspace");
        _outsideDir = Path.Combine(baseDir, "outside");
        Directory.CreateDirectory(_workspace);
        Directory.CreateDirectory(_outsideDir);

        _secretFile = Path.Combine(_outsideDir, "secret.txt");
        File.WriteAllText(_secretFile, "SECRET");
    }

    private static JsonElement Input(object payload) => JsonSerializer.SerializeToElement(payload);

    [Fact]
    public async Task ReadFile_OdmitneUnikPresDvojtecku()
    {
        var tool = new ReadFileTool(_workspace);
        var relative = Path.GetRelativePath(_workspace, _secretFile); // ..\outside\secret.txt

        var result = await tool.ExecuteAsync(Input(new { path = relative }));

        Assert.StartsWith("Chyba:", result);
        Assert.DoesNotContain("SECRET", result);
    }

    [Fact]
    public async Task ReadFile_OdmitneAbsolutniCestuMimoWorkspace()
    {
        var tool = new ReadFileTool(_workspace);

        var result = await tool.ExecuteAsync(Input(new { path = _secretFile }));

        Assert.StartsWith("Chyba:", result);
        Assert.DoesNotContain("SECRET", result);
    }

    [Fact]
    public async Task ReadFile_PovoliSouborUvnitrWorkspace()
    {
        await File.WriteAllTextAsync(Path.Combine(_workspace, "uvnitr.txt"), "OBSAH");
        var tool = new ReadFileTool(_workspace);

        var result = await tool.ExecuteAsync(Input(new { path = "uvnitr.txt" }));

        Assert.Equal("OBSAH", result);
    }

    [Fact]
    public async Task WriteFile_OdmitneZapisMimoWorkspace()
    {
        var tool = new WriteFileTool(_workspace);
        var name = $"escaped-{Guid.NewGuid():N}.txt";
        var escapedTarget = Path.GetFullPath(Path.Combine("..", name), _workspace);

        var result = await tool.ExecuteAsync(Input(new { path = Path.Combine("..", name), content = "DATA" }));

        Assert.StartsWith("Chyba:", result);
        Assert.False(File.Exists(escapedTarget), "Soubor mimo workspace nesmí vzniknout.");
    }

    [Fact]
    public async Task ListFiles_OdmitneAdresarMimoWorkspace()
    {
        var tool = new ListFilesTool(_workspace);

        var result = await tool.ExecuteAsync(Input(new { path = _outsideDir }));

        Assert.StartsWith("Chyba:", result);
        Assert.DoesNotContain("secret.txt", result);
    }

    [Fact]
    public async Task ReadFile_OdmitneSymlinkMimoWorkspace()
    {
        var linkPath = Path.Combine(_workspace, "odkaz-na-secret.txt");
        try
        {
            File.CreateSymbolicLink(linkPath, _secretFile);
        }
        catch
        {
            // Tvorba symlinku vyžaduje na Windows Developer Mode / admin práva.
            // Bez nich test přeskočíme, aby zbytečně nepadal.
            return;
        }

        var tool = new ReadFileTool(_workspace);

        var result = await tool.ExecuteAsync(Input(new { path = "odkaz-na-secret.txt" }));

        Assert.StartsWith("Chyba:", result);
        Assert.DoesNotContain("SECRET", result);
    }

    public void Dispose()
    {
        var baseDir = Path.GetDirectoryName(_workspace);
        if (baseDir is not null && Directory.Exists(baseDir))
        {
            Directory.Delete(baseDir, recursive: true);
        }
    }
}

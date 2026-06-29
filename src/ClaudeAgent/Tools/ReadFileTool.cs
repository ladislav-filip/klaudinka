using System.Text;
using System.Text.Json;

namespace ClaudeAgent.Tools;

/// <summary>
/// Nástroj pro čtení textového souboru z disku.
/// Omezení: maximálně 100 KB, pouze textové soubory.
/// </summary>
public sealed class ReadFileTool : ITool
{
  private const int MaxFileSizeBytes = 100 * 1024;

  private readonly string _workspaceRoot;

  /// <param name="workspaceRoot">Kořen, mimo který nesmí tool číst (sandbox).</param>
  public ReadFileTool(string workspaceRoot)
  {
    _workspaceRoot = WorkspaceGuard.NormalizeRoot(workspaceRoot);
  }

  public string Name => "read_file";

  public string Description =>
      "Přečte obsah textového souboru. Vrací obsah jako text nebo chybovou zprávu.";

  public object InputSchema => new
  {
    type = "object",
    properties = new
    {
      path = new
      {
        type = "string",
        description = "Relativní nebo absolutní cesta k souboru."
      }
    },
    required = new[] { "path" }
  };

  public async Task<string> ExecuteAsync(JsonElement input)
  {
    if (!input.TryGetProperty("path", out var pathElement) || pathElement.ValueKind != JsonValueKind.String)
    {
      return "Chyba: parametr 'path' je povinný a musí být řetězec.";
    }

    var path = pathElement.GetString();
    if (string.IsNullOrWhiteSpace(path))
    {
      return "Chyba: cesta k souboru nesmí být prázdná.";
    }

    try
    {
      if (!WorkspaceGuard.TryResolve(_workspaceRoot, path, out var fullPath, out var error))
      {
        return error;
      }

      if (!File.Exists(fullPath))
      {
        return $"Chyba: soubor '{path}' neexistuje.";
      }

      var fileInfo = new FileInfo(fullPath);
      if (fileInfo.Length > MaxFileSizeBytes)
      {
        return $"Chyba: soubor překračuje maximální velikost {MaxFileSizeBytes / 1024} KB.";
      }

      if (!IsTextFile(fullPath))
      {
        return "Chyba: soubor není textový nebo obsahuje binární data.";
      }

      return await File.ReadAllTextAsync(fullPath, Encoding.UTF8);
    }
    catch (Exception ex)
    {
      return $"Chyba při čtení souboru: {ex.Message}";
    }
  }

  /// <summary>
  /// Heuristika pro detekci textového souboru — kontroluje NUL bajty v prvních 8 KB.
  /// </summary>
  internal static bool IsTextFile(string path)
  {
    using var stream = File.OpenRead(path);
    var buffer = new byte[Math.Min(8192, stream.Length)];
    var read = stream.Read(buffer, 0, buffer.Length);

    for (var i = 0; i < read; i++)
    {
      if (buffer[i] == 0)
      {
        return false;
      }
    }

    return true;
  }
}

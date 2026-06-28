using System.Text;
using System.Text.Json;

namespace ClaudeAgent.Tools;

/// <summary>
/// Nástroj pro zápis nebo přepsání textového souboru.
/// Vytvoří nadřazené adresáře, pokud neexistují.
/// </summary>
public sealed class WriteFileTool : ITool
{
  private readonly string _workspaceRoot;

  /// <param name="workspaceRoot">Kořen, mimo který nesmí tool zapisovat (sandbox).</param>
  public WriteFileTool(string workspaceRoot)
  {
    _workspaceRoot = WorkspaceGuard.NormalizeRoot(workspaceRoot);
  }

  public string Name => "write_file";

  public string Description =>
      "Zapíše nebo přepíše textový soubor. Vytvoří adresáře v cestě, pokud neexistují.";

  public object InputSchema => new
  {
    type = "object",
    properties = new
    {
      path = new
      {
        type = "string",
        description = "Relativní nebo absolutní cesta k souboru."
      },
      content = new
      {
        type = "string",
        description = "Textový obsah k zapsání."
      }
    },
    required = new[] { "path", "content" }
  };

  public async Task<string> ExecuteAsync(JsonElement input)
  {
    if (!input.TryGetProperty("path", out var pathElement) || pathElement.ValueKind != JsonValueKind.String)
    {
      return "Chyba: parametr 'path' je povinný a musí být řetězec.";
    }

    if (!input.TryGetProperty("content", out var contentElement) || contentElement.ValueKind != JsonValueKind.String)
    {
      return "Chyba: parametr 'content' je povinný a musí být řetězec.";
    }

    var path = pathElement.GetString();
    if (string.IsNullOrWhiteSpace(path))
    {
      return "Chyba: cesta k souboru nesmí být prázdná.";
    }

    var content = contentElement.GetString() ?? string.Empty;

    try
    {
      if (!WorkspaceGuard.TryResolve(_workspaceRoot, path, out var fullPath, out var error))
      {
        return error;
      }

      var directory = Path.GetDirectoryName(fullPath);

      if (!string.IsNullOrEmpty(directory))
      {
        Directory.CreateDirectory(directory);
      }

      await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8);
      return $"Soubor '{path}' byl úspěšně zapsán ({content.Length} znaků).";
    }
    catch (Exception ex)
    {
      return $"Chyba při zápisu souboru: {ex.Message}";
    }
  }
}

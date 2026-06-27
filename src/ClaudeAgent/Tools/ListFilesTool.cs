using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeAgent.Tools;

/// <summary>
/// Nástroj pro výpis souborů v adresáři s volitelným glob patternem.
/// Vrací JSON seznam s velikostí a datem poslední změny.
/// </summary>
public sealed class ListFilesTool : ITool
{
  public string Name => "list_files";

  public string Description =>
      "Vypíše soubory v adresáři. Vrací JSON seznam s velikostí a datem změny.";

  public object InputSchema => new
  {
    type = "object",
    properties = new
    {
      path = new
      {
        type = "string",
        description = "Cesta k adresáři."
      },
      pattern = new
      {
        type = "string",
        description = "Volitelný glob pattern pro filtrování souborů (výchozí: *)."
      }
    },
    required = new[] { "path" }
  };

  public Task<string> ExecuteAsync(JsonElement input)
  {
    if (!input.TryGetProperty("path", out var pathElement) || pathElement.ValueKind != JsonValueKind.String)
    {
      return Task.FromResult("Chyba: parametr 'path' je povinný a musí být řetězec.");
    }

    var path = pathElement.GetString();
    if (string.IsNullOrWhiteSpace(path))
    {
      return Task.FromResult("Chyba: cesta k adresáři nesmí být prázdná.");
    }

    var pattern = "*";
    if (input.TryGetProperty("pattern", out var patternElement) && patternElement.ValueKind == JsonValueKind.String)
    {
      pattern = patternElement.GetString() ?? "*";
    }

    try
    {
      var fullPath = Path.GetFullPath(path);

      if (!Directory.Exists(fullPath))
      {
        return Task.FromResult($"Chyba: adresář '{path}' neexistuje.");
      }

      var entries = Directory
          .EnumerateFiles(fullPath, pattern)
          .Select(filePath =>
          {
            var info = new FileInfo(filePath);
            return new FileEntry
            {
              Name = info.Name,
              Path = Path.GetRelativePath(Directory.GetCurrentDirectory(), info.FullName),
              SizeBytes = info.Length,
              LastModified = info.LastWriteTimeUtc
            };
          })
          .OrderBy(e => e.Name)
          .ToList();

      var json = JsonSerializer.Serialize(entries, JsonOptions);
      return Task.FromResult(json);
    }
    catch (Exception ex)
    {
      return Task.FromResult($"Chyba při výpisu souborů: {ex.Message}");
    }
  }

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    WriteIndented = true
  };

  /// <summary>DTO pro serializaci výsledku list_files.</summary>
  private sealed class FileEntry
  {
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("size_bytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("last_modified")]
    public DateTime LastModified { get; set; }
  }
}

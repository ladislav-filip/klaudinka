# ClaudeAgent API Reference

Interní reference pro vývojáře pracující s kódem projektu.

## ITool

```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }
    object InputSchema { get; }
    Task<string> ExecuteAsync(JsonElement input);
}
```

Každý nástroj vrací výsledek jako řetězec. Chyby začínají prefixem `Chyba:` — agentní smyčka je považuje za neúspěšné volání.

## Modely

### ApiRequest

Odesílaný payload na Messages API. Pole `tools` obsahuje definice z `ToolRegistry.GetToolDefinitions()`.

### ApiResponse

Odpověď API. Klíčové pole je `stop_reason`:

- `end_turn` — model dokončil odpověď
- `tool_use` — model požaduje volání nástrojů

### Message

Pole `content` podporuje dva formáty:

- `string` — jednoduchá textová zpráva uživatele
- `List<ContentBlock>` — strukturovaný obsah (tool_use, tool_result, text)

Serializaci zajišťuje `MessageJsonConverter`.

### ContentBlock

| type | Použití | Klíčová pole |
|------|---------|---------------|
| `text` | Textová odpověď | `text` |
| `tool_use` | Požadavek na nástroj | `id`, `name`, `input` |
| `tool_result` | Výsledek nástroje | `tool_use_id`, `content` |

## AgentLoop události

```csharp
event EventHandler<ToolCallEventArgs>? ToolCallStarted;
event EventHandler<ToolCompletedEventArgs>? ToolCallCompleted;
```

Používají se v `Program.cs` pro výpis průběhu (`🔧 Volám tool...`, `✅ Tool dokončen`).

## AnthropicClient

```csharp
public Task<ApiResponse> SendAsync(ApiRequest request, CancellationToken cancellationToken = default)
```

Při neúspěšném HTTP statusu vyhodí `HttpRequestException` s tělem odpovědi.

Pro testování lze předat vlastní `HttpClient` s mock handlerem (viz `tests/ClaudeAgent.Tests/Helpers/`).

## JSON Schema nástrojů

### read_file

```json
{
  "type": "object",
  "properties": {
    "path": { "type": "string", "description": "Cesta k souboru." }
  },
  "required": ["path"]
}
```

### write_file

```json
{
  "type": "object",
  "properties": {
    "path": { "type": "string" },
    "content": { "type": "string" }
  },
  "required": ["path", "content"]
}
```

### list_files

```json
{
  "type": "object",
  "properties": {
    "path": { "type": "string" },
    "pattern": { "type": "string", "description": "Glob pattern, výchozí *" }
  },
  "required": ["path"]
}
```

Výstup `list_files` je JSON pole objektů:

```json
[
  {
    "name": "Program.cs",
    "path": "src/ClaudeAgent/Program.cs",
    "size_bytes": 2400,
    "last_modified": "2026-06-27T10:00:00Z"
  }
]
```

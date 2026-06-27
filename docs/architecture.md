# ClaudeAgent — Dokumentace

Konzolová aplikace v .NET 8 demonstrující agentní smyčku s Claude (Anthropic API).

## Struktura repozitáře

```
ClaudeAgent/
├── src/
│   └── ClaudeAgent/          # Hlavní aplikace
│       ├── Program.cs        # REPL vstupní bod
│       ├── AnthropicClient.cs
│       ├── AgentLoop.cs
│       ├── Models/
│       └── Tools/
├── tests/
│   └── ClaudeAgent.Tests/    # Unit testy (xUnit)
├── docs/                     # Dokumentace projektu
├── ClaudeAgent.sln
└── README.md                 # Zadání a specifikace
```

## Architektura

```mermaid
flowchart TD
    User[Uživatel REPL] --> Program
    Program --> AgentLoop
    AgentLoop --> AnthropicClient
    AnthropicClient --> API[Anthropic Messages API]
    AgentLoop --> ToolRegistry
    ToolRegistry --> ReadFile[read_file]
    ToolRegistry --> WriteFile[write_file]
    ToolRegistry --> ListFiles[list_files]
    API -->|tool_use| AgentLoop
    AgentLoop -->|tool_result| API
    API -->|end_turn| Program
```

## Komponenty

### AnthropicClient

Přímé HTTP volání endpointu `POST https://api.anthropic.com/v1/messages` bez externího SDK.
Serializace probíhá přes `System.Text.Json` s vlastním konvertorem pro pole `Message.content`.

### AgentLoop

Implementuje smyčku tool calling:

1. Odešle zprávy na API včetně definic nástrojů
2. Při `stop_reason == "tool_use"` vykoná lokální nástroje a přidá `tool_result`
3. Při `stop_reason == "end_turn"` vrátí textovou odpověď

### Nástroje (Tools)

| Nástroj | Účel | Omezení |
|---------|------|---------|
| `read_file` | Čtení textového souboru | Max 100 KB, bez binárních dat |
| `write_file` | Zápis textového souboru | Vytvoří adresáře v cestě |
| `list_files` | Výpis souborů v adresáři | Volitelný glob pattern |

## Spuštění

### Požadavky

- .NET 8 SDK nebo novější
- Environment proměnná `ANTHROPIC_API_KEY`

### Build a run

```bash
export ANTHROPIC_API_KEY="sk-ant-..."
dotnet run --project src/ClaudeAgent
```

### Testy

```bash
dotnet test
```

## Konfigurace

| Parametr | Zdroj | Výchozí hodnota |
|----------|-------|-----------------|
| API klíč | `ANTHROPIC_API_KEY` | — (povinné) |
| Model | hardcoded | `claude-sonnet-4-6` |
| max_tokens | hardcoded | `8096` |

## REPL příkazy

| Příkaz | Popis |
|--------|-------|
| `exit` | Ukončí aplikaci |
| `clear` | Resetuje historii konverzace |
| `help` | Zobrazí nápovědu |
| libovolný text | Odešle zprávu agentovi |

## Rozšíření (backlog)

Viz tabulku v `README.md` — plánované featury zahrnují `execute_command`, konfiguraci přes `appsettings.json`, logování a streaming.

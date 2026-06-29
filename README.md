# ClaudeAgent CLI — Zadání implementace

> Cíl: Jednoduchá konzolová aplikace v .NET Core demonstrující agentní smyčku s Claude (Anthropic API). Bez externích NuGet balíčků kromě základních .NET knihoven. Slouží jako základ pro budoucí harness.

---

## Struktura repozitáře

```
ClaudeAgent/
├── src/
│   └── ClaudeAgent/            ← hlavní aplikace (.NET 10)
├── tests/
│   └── ClaudeAgent.Tests/      ← unit testy (xUnit)
├── docs/                       ← architektura, API reference a konfigurace
├── ClaudeAgent.sln
└── README.md
```

## Přehled aplikace

```
src/ClaudeAgent/
├── ClaudeAgent.csproj
├── appsettings.json            ← konfigurace (bez secretů)
├── Program.cs                  ← vstupní bod, REPL smyčka
├── AnthropicClient.cs          ← HTTP klient pro Anthropic API
├── AgentLoop.cs                ← agentní smyčka (tool calling loop)
├── Configuration/
│   ├── AgentSettings.cs        ← POCO konfigurace
│   └── AgentConfiguration.cs   ← načítání konfigurace (json + env)
├── Models/
│   ├── Message.cs              ← request/response modely
│   ├── ContentBlock.cs         ← bloky obsahu zpráv
│   └── Tool.cs                 ← definice toolů a API modelů
└── Tools/
    ├── ITool.cs                ← rozhraní nástroje
    ├── ToolRegistry.cs         ← registr dostupných toolů
    ├── ReadFileTool.cs         ← čtení souboru
    ├── WriteFileTool.cs        ← zápis souboru
    └── ListFilesTool.cs        ← výpis souborů v adresáři
```

### Spuštění

```bash
export ANTHROPIC_API_KEY="sk-ant-..."
dotnet run --project src/ClaudeAgent
dotnet test
```

Podrobnější dokumentace: [`docs/architecture.md`](docs/architecture.md), [`docs/api-reference.md`](docs/api-reference.md), [`docs/configuration.md`](docs/configuration.md).

---

## Specifikace komponent

### 1. `AnthropicClient.cs`

Přímé volání Anthropic Messages API přes `HttpClient`. **Žádný SDK.**

- Endpoint: `POST https://api.anthropic.com/v1/messages`
- Hlavičky:
  - `x-api-key: {API_KEY}`
  - `anthropic-version: 2023-06-01`
  - `content-type: application/json`
- Serializace/deserializace: `System.Text.Json`
- Podporuje posílání `tools` a `tool_result` zpráv

```
Metody:
  Task<ApiResponse> SendAsync(ApiRequest request)
```

---

### 2. `AgentLoop.cs`

Implementuje **agentní smyčku** — opakuje volání API dokud Claude neskončí odpovědí bez tool_use.

```
Smyčka:
  1. Pošli zprávy na API
  2. Pokud response.stop_reason == "tool_use":
       a. Najdi tool_use bloky v response
       b. Vykonej příslušný tool
       c. Přidej tool_result do konverzace
       d. Opakuj od 1
  3. Pokud response.stop_reason == "end_turn":
       Vrať výslednou text odpověď
```

---

### 3. `Program.cs` — REPL smyčka

Jednoduchá interaktivní smyčka v terminálu:

```
Chování:
  - Zobrazí uvítání a seznam dostupných toolů
  - Čeká na vstup uživatele ("> ")
  - Speciální příkazy: "exit", "clear" (reset konverzace), "help"
  - Udržuje historii konverzace (seznam zpráv) pro celé sezení
  - Zobrazuje průběh: "🔧 Volám tool: read_file..."
  - Zobrazuje výslednou odpověď Claude
```

---

### 4. Tooly

Každý tool implementuje interface:

```csharp
interface ITool
{
    string Name { get; }
    string Description { get; }
    object InputSchema { get; }   // JSON Schema pro parametry
    Task<string> ExecuteAsync(JsonElement input);
}
```

#### `read_file`
- **Popis:** Přečte obsah textového souboru
- **Parametry:** `path` (string, required) — relativní nebo absolutní cesta
- **Vrací:** Obsah souboru jako string, nebo chybovou zprávu
- **Omezení:** Max 100 KB, jen textové soubory

#### `write_file`
- **Popis:** Zapíše nebo přepíše textový soubor
- **Parametry:**
  - `path` (string, required) — cesta k souboru
  - `content` (string, required) — obsah k zapsání
- **Vrací:** Potvrzení zápisu nebo chybovou zprávu
- **Chování:** Vytvoří adresář pokud neexistuje

#### `list_files`
- **Popis:** Vypíše soubory v adresáři
- **Parametry:**
  - `path` (string, required) — cesta k adresáři
  - `pattern` (string, optional) — glob pattern, default `*`
- **Vrací:** JSON seznam souborů s velikostí a datem změny

---

### 5. `Models/`

#### `ApiRequest`
```
- model: string             ("claude-sonnet-4-6")
- max_tokens: int           (8096)
- system: string            (system prompt)
- messages: List<Message>
- tools: List<ToolDefinition>
```

#### `Message`
```
- role: string              ("user" | "assistant")
- content: object           (string nebo List<ContentBlock>)
```

#### `ContentBlock`
```
- type: string              ("text" | "tool_use" | "tool_result")
- id: string?               (pro tool_use a tool_result)
- name: string?             (pro tool_use)
- input: JsonElement?       (pro tool_use)
- content: string?          (pro tool_result)
- tool_use_id: string?      (pro tool_result)
- text: string?             (pro text)
```

---

## System prompt

```
Jsi AI asistent specializovaný na práci se soubory.
Máš přístup k nástrojům pro čtení, zápis a výpis souborů.
Vždy potvrď akci kterou jsi provedl.
Pokud soubor neexistuje nebo nastane chyba, informuj uživatele.
Pracovní adresář je aktuální adresář odkud je aplikace spuštěna.
```

---

## Konfigurace

Konfigurace se skládá z `appsettings.json`, volitelného lokálního override
`appsettings.local.json` a proměnných prostředí (přes `Microsoft.Extensions.Configuration`).
Kompletní popis vrstev, priorit a parametrů: [`docs/configuration.md`](docs/configuration.md).

| Parametr | appsettings klíč | Výchozí |
|----------|------------------|---------|
| Model | `ClaudeAgent:Model` | `claude-sonnet-4-6` |
| Max. tokenů | `ClaudeAgent:MaxTokens` | `8096` |
| Max. iterací nástrojů | `ClaudeAgent:MaxToolIterations` | `25` |
| Workspace | `ClaudeAgent:Workspace` | aktuální adresář |

> **Bezpečnost:** API klíč je secret a načítá se **výhradně** z proměnné prostředí
> `ANTHROPIC_API_KEY` — nikdy z konfiguračního souboru. Pokud není nastaven,
> aplikace vypíše chybu a skončí.

---

## Technické požadavky

- **.NET 10** nebo novější
- Minimum externích závislostí — jádro (Anthropic API) běží přes `System.Text.Json` bez SDK;
  konfigurace používá `Microsoft.Extensions.Configuration.*`
- `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`
- Async/await všude kde je I/O

---

## Ukázkové scénáře k otestování

Po spuštění (`dotnet run`) otestuj tyto vstupy:

```
> Vypiš soubory v aktuálním adresáři
> Přečti soubor Program.cs
> Vytvoř soubor hello.txt s obsahem "Ahoj světe!"
> Přečti soubor hello.txt a přelož obsah do angličtiny, výsledek ulož do hello_en.txt
> Vezmi všechny .cs soubory v adresáři a vytvoř soubor summary.md s jejich přehledem
```

Poslední scénář demonstruje vícekolovou agentní smyčku — Claude zavolá více toolů v sérii.

---

## Rozšíření v dalších iteracích (backlog)

| Priorita | Feature |
|----------|---------|
| P1 | `execute_command` tool — spuštění shell příkazu |
| ✅ hotovo | ~~Konfigurace přes `appsettings.json`~~ — viz [`docs/configuration.md`](docs/configuration.md) |
| P2 | Logování tool calls do souboru |
| P2 | `search_files` tool — grep přes soubory |
| P3 | Streaming odpovědí (Server-Sent Events) |
| P3 | Více konverzačních sezení / historie |
| P3 | Podpora obrázků (vision) |

---

## Očekávaný výstup při spuštění

```
╔══════════════════════════════════════╗
║        ClaudeAgent CLI v0.1          ║
║  Model: claude-sonnet-4-6            ║
╚══════════════════════════════════════╝

Dostupné tooly: read_file, write_file, list_files
Příkazy: exit | clear (reset konverzace) | help

> Vypiš soubory v aktuálním adresáři

🔧 Volám tool: list_files {"path": "."}
✅ Tool dokončen

V aktuálním adresáři se nachází tyto soubory:
- ClaudeAgent.csproj (1.2 KB)
- Program.cs (2.4 KB)
- AgentLoop.cs (3.1 KB)
...

>
```

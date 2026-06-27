# Code review PR #1 — nálezy

> Review model: GPT 5.5  
> Rozsah: `a75e40b..a51634d` · 26 souborů, +1726 řádků  
> Ověření: `dotnet build` ✅ · `dotnet test` 18/18 ✅

---

## Silné stránky

- Architektura odpovídá zadání: `Program` → `AgentLoop` → `AnthropicClient` → tools
- `AnthropicClient` volá API přímo přes `HttpClient` bez SDK, správný endpoint a hlavičky (`src/ClaudeAgent/AnthropicClient.cs:41-49`)
- Model `claude-sonnet-4-6`, `max_tokens = 8096`, REPL příkazy a výpis průběhu toolů implementovány
- Agentní smyčka má správný základní tvar: `tool_use` → lokální tool → `tool_result` → další API request
- 18 unit testů pokrývá nástroje, serializaci a základní flow `AgentLoop`

---

## Nálezy

### C1 — Critical: Neomezený přístup k filesystemu (path traversal)

**Soubory:**
- `src/ClaudeAgent/Tools/ReadFileTool.cs:48-66`
- `src/ClaudeAgent/Tools/WriteFileTool.cs:58-66`
- `src/ClaudeAgent/Tools/ListFilesTool.cs:57-65`

**Problém:** `read_file`, `write_file` i `list_files` používají pouze `Path.GetFullPath()` bez omezení na pracovní adresář. Model může požádat o čtení/zápis mimo workspace (např. `../../.env`, `/home/user/.ssh/id_rsa`).

**Doporučení:** zavést workspace root, normalizovat cílovou cestu a odmítnout vše mimo něj. Přidat testy pro `../`, absolutní cesty mimo root a symlinky.

---

### I1 — Important: Agentní smyčka bez limitu iterací

**Soubor:** `src/ClaudeAgent/AgentLoop.cs:65-89`, `106-142`

**Problém:** `while (true)` nemá `maxToolIterations` / budget guard. Při `tool_use` bez `tool_use` bloků se odešle prázdný `tool_result` a smyčka pokračuje.

**Doporučení:** přidat limit iterací, explicitní chybu pro `tool_use` bez bloků, testy na nekonečný loop.

---

### I2 — Important: `read_file` nesplňuje robustně požadavek „text only“

**Soubor:** `src/ClaudeAgent/Tools/ReadFileTool.cs:61-66`, `77-92`

**Problém:** Binární detekce kontroluje pouze NUL bajty v prvních 8 KB. Binární soubor bez NUL bajtů nebo invalidní UTF-8 může projít.

**Doporučení:** strict UTF-8 decoder s `throwOnInvalidBytes: true`, případně allowlist přípon.

---

### I3 — Important: Chybí přímé testy `AnthropicClient`

**Soubor:** `src/ClaudeAgent/AnthropicClient.cs:41-49`, `tests/ClaudeAgent.Tests/AgentLoopTests.cs:15-56`

**Problém:** Kritická integrační logika (endpoint, hlavičky, JSON body) není přímo testována.

**Doporučení:** přidat unit testy přímo pro `AnthropicClient.SendAsync`.

---

### M1 — Minor: `tool_result` neumí `is_error`

**Soubor:** `src/ClaudeAgent/Models/ContentBlock.cs:47-52`, `src/ClaudeAgent/AgentLoop.cs:132-137`

**Problém:** Úspěch/neúspěch se používá jen pro event, ale `tool_result` neumí nést `is_error`.

---

### M2 — Minor: Chybějící validace `tool_use.id`

**Soubor:** `src/ClaudeAgent/AgentLoop.cs:139`

**Problém:** Při chybějícím `block.Id` se odešle prázdný `tool_use_id` → pravděpodobné odmítnutí requestu API.

---

### M3 — Minor: `AnthropicClient` disposuje i injektovaný `HttpClient`

**Soubor:** `src/ClaudeAgent/AnthropicClient.cs:27-35`, `66`

**Problém:** `Dispose()` vždy likviduje klienta, i když byl předán zvenku.

---

## Doporučení před mergem

1. Sandboxing cest pro všechny file tooly (priorita #1)
2. Limit agentní smyčky + testy na repeated/nekonečný `tool_use`
3. Unit testy `AnthropicClient` — URL, method, headers, body
4. Zpřísnit text-only detekci v `read_file`
5. Rozšířit testy o: path traversal, soubor >100 KB, invalid UTF-8, multiple `tool_use` bloky, neznámý tool, chybějící `tool_use.id`

---

## Verdikt

**Ready to merge: With fixes**

Před mergem je nutné opravit minimálně **C1 (filesystem sandboxing)** a **I1 (limit agentní smyčky)**.

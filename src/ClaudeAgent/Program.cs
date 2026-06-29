using ClaudeAgent.Configuration;
using ClaudeAgent.Models;
using ClaudeAgent.Tools;

namespace ClaudeAgent;

/// <summary>
/// Vstupní bod konzolové aplikace — interaktivní REPL smyčka s agentní logikou.
/// </summary>
public static class Program
{
    private const string SystemPrompt =
        """
        Jsi AI asistent specializovaný na práci se soubory.
        Máš přístup k nástrojům pro čtení, zápis a výpis souborů.
        Vždy potvrď akci kterou jsi provedl.
        Pokud soubor neexistuje nebo nastane chyba, informuj uživatele.
        Přístup k souborům je omezen na pracovní adresář (workspace); cesty mimo
        něj jsou z bezpečnostních důvodů odmítnuty. Workspace je adresář, odkud je
        aplikace spuštěna (lze přepsat proměnnou prostředí CLAUDE_AGENT_WORKSPACE).
        """;

    public static async Task<int> Main(string[] args)
    {
        // API klíč je secret — výhradně z proměnné prostředí, nikdy z konfiguračního souboru.
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("Chyba: environment proměnná ANTHROPIC_API_KEY není nastavena.");
            return 1;
        }

        var settings = AgentConfiguration.Load(AppContext.BaseDirectory, Environment.GetEnvironmentVariable);

        var workspaceRoot = ResolveWorkspaceRoot(settings.Workspace);
        var maxIterations = NormalizePositive(settings.MaxToolIterations, AgentLoop.DefaultMaxToolIterations, nameof(settings.MaxToolIterations));
        var maxTokens = NormalizePositive(settings.MaxTokens, AgentLoop.DefaultMaxTokens, nameof(settings.MaxTokens));

        var registry = new ToolRegistry([
            new ReadFileTool(workspaceRoot),
            new WriteFileTool(workspaceRoot),
            new ListFilesTool(workspaceRoot)
        ]);

        Console.WriteLine($"Pracovní adresář (workspace): {workspaceRoot}");
        Console.WriteLine($"Model: {settings.Model}");
        Console.WriteLine($"Max tokenů: {maxTokens} · max iterací nástrojů: {maxIterations}");

        using var client = new AnthropicClient(apiKey);
        var agent = new AgentLoop(client, registry, SystemPrompt, settings.Model, maxIterations, maxTokens);

        agent.ToolCallStarted += (_, e) =>
            Console.WriteLine($"\n🔧 Volám tool: {e.ToolName} {e.InputJson}");

        agent.ToolCallCompleted += (_, e) =>
            Console.WriteLine(e.Success ? "✅ Tool dokončen" : "⚠️ Tool dokončen s chybou");

        var conversation = new List<Message>();

        PrintWelcome(registry, settings.Model);

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (input is null)
            {
                break;
            }

            var trimmed = input.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            switch (trimmed.ToLowerInvariant())
            {
                case "exit":
                    return 0;
                case "clear":
                    conversation.Clear();
                    Console.WriteLine("Konverzace byla resetována.\n");
                    continue;
                case "help":
                    PrintHelp(registry);
                    continue;
            }

            try
            {
                var response = await agent.ProcessUserInputAsync(conversation, trimmed);
                Console.WriteLine();
                Console.WriteLine(response);
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"Chyba: {ex.Message}");
                Console.WriteLine();
            }
        }

        return 0;
    }

    /// <summary>
    /// Zjistí workspace root z konfigurace — prázdná hodnota znamená aktuální adresář.
    /// </summary>
    private static string ResolveWorkspaceRoot(string? configured)
    {
        var root = string.IsNullOrWhiteSpace(configured)
            ? Directory.GetCurrentDirectory()
            : configured;

        return WorkspaceGuard.NormalizeRoot(root);
    }

    /// <summary>
    /// Ošetří nekladné hodnoty z konfigurace — vrátí výchozí a upozorní uživatele,
    /// aby aplikace nespadla na validaci v <see cref="AgentLoop"/>.
    /// </summary>
    private static int NormalizePositive(int value, int fallback, string name)
    {
        if (value > 0)
        {
            return value;
        }

        Console.Error.WriteLine($"Varování: neplatná hodnota konfigurace '{name}' ({value}), použije se výchozí {fallback}.");
        return fallback;
    }

    private static void PrintWelcome(ToolRegistry registry, string model)
    {
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║        ClaudeAgent CLI v0.1          ║");
        Console.WriteLine($"║  Model: {model,-28} ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"Dostupné tooly: {string.Join(", ", registry.ToolNames)}");
        Console.WriteLine("Příkazy: exit | clear (reset konverzace) | help");
        Console.WriteLine();
    }

    private static void PrintHelp(ToolRegistry registry)
    {
        Console.WriteLine();
        Console.WriteLine("ClaudeAgent CLI — nápověda");
        Console.WriteLine("──────────────────────────");
        Console.WriteLine("Zadejte libovolný příkaz v přirozeném jazyce.");
        Console.WriteLine("Agent použije dostupné nástroje pro práci se soubory.");
        Console.WriteLine();
        Console.WriteLine("Dostupné nástroje:");
        foreach (var name in registry.ToolNames.OrderBy(n => n))
        {
            Console.WriteLine($"  • {name}");
        }
        Console.WriteLine();
        Console.WriteLine("Speciální příkazy:");
        Console.WriteLine("  exit  — ukončí aplikaci");
        Console.WriteLine("  clear — vymaže historii konverzace");
        Console.WriteLine("  help  — zobrazí tuto nápovědu");
        Console.WriteLine();
    }
}

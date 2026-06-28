using ClaudeAgent.Models;
using ClaudeAgent.Tools;

namespace ClaudeAgent;

/// <summary>
/// Vstupní bod konzolové aplikace — interaktivní REPL smyčka s agentní logikou.
/// </summary>
public static class Program
{
    private const string Model = "claude-sonnet-4-6";

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
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("Chyba: environment proměnná ANTHROPIC_API_KEY není nastavena.");
            return 1;
        }

        var workspaceRoot = ResolveWorkspaceRoot();
        var maxIterations = ResolveMaxIterations();

        var registry = new ToolRegistry([
            new ReadFileTool(workspaceRoot),
            new WriteFileTool(workspaceRoot),
            new ListFilesTool(workspaceRoot)
        ]);

        Console.WriteLine($"Pracovní adresář (workspace): {workspaceRoot}");
        Console.WriteLine($"Max iterací nástrojů: {maxIterations}");

        using var client = new AnthropicClient(apiKey);
        var agent = new AgentLoop(client, registry, SystemPrompt, Model, maxIterations);

        agent.ToolCallStarted += (_, e) =>
            Console.WriteLine($"\n🔧 Volám tool: {e.ToolName} {e.InputJson}");

        agent.ToolCallCompleted += (_, e) =>
            Console.WriteLine(e.Success ? "✅ Tool dokončen" : "⚠️ Tool dokončen s chybou");

        var conversation = new List<Message>();

        PrintWelcome(registry);

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
    /// Zjistí workspace root — výchozí je aktuální adresář, lze přepsat
    /// proměnnou prostředí CLAUDE_AGENT_WORKSPACE.
    /// </summary>
    private static string ResolveWorkspaceRoot()
    {
        var configured = Environment.GetEnvironmentVariable("CLAUDE_AGENT_WORKSPACE");
        var root = string.IsNullOrWhiteSpace(configured)
            ? Directory.GetCurrentDirectory()
            : configured;

        return WorkspaceGuard.NormalizeRoot(root);
    }

    /// <summary>
    /// Zjistí maximální počet iterací nástrojů — výchozí je
    /// <see cref="AgentLoop.DefaultMaxToolIterations"/>, lze přepsat kladnou hodnotou
    /// v proměnné prostředí CLAUDE_AGENT_MAX_ITERATIONS.
    /// </summary>
    private static int ResolveMaxIterations()
    {
        var configured = Environment.GetEnvironmentVariable("CLAUDE_AGENT_MAX_ITERATIONS");
        if (int.TryParse(configured, out var value) && value > 0)
        {
            return value;
        }

        return AgentLoop.DefaultMaxToolIterations;
    }

    private static void PrintWelcome(ToolRegistry registry)
    {
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║        ClaudeAgent CLI v0.1          ║");
        Console.WriteLine($"║  Model: {Model,-28} ║");
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

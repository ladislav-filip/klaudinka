namespace ClaudeAgent.Configuration;

/// <summary>
/// Konfigurace agenta načtená z appsettings.json a proměnných prostředí.
/// API klíč zde záměrně NENÍ — jde o secret načítaný výhradně z proměnné
/// prostředí ANTHROPIC_API_KEY, nikdy z konfiguračního souboru.
/// </summary>
public sealed class AgentSettings
{
    /// <summary>Název konfigurační sekce v appsettings.json.</summary>
    public const string SectionName = "ClaudeAgent";

    /// <summary>Model Anthropic API.</summary>
    public string Model { get; set; } = AgentLoop.DefaultModel;

    /// <summary>Maximální počet tokenů v odpovědi.</summary>
    public int MaxTokens { get; set; } = AgentLoop.DefaultMaxTokens;

    /// <summary>Maximální počet kol volání nástrojů v jednom uživatelském požadavku.</summary>
    public int MaxToolIterations { get; set; } = AgentLoop.DefaultMaxToolIterations;

    /// <summary>Pracovní adresář (workspace). Prázdné = aktuální adresář.</summary>
    public string? Workspace { get; set; }
}

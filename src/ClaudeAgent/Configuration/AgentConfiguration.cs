using Microsoft.Extensions.Configuration;

namespace ClaudeAgent.Configuration;

/// <summary>
/// Načítá <see cref="AgentSettings"/> z více konfiguračních vrstev.
/// </summary>
public static class AgentConfiguration
{
    /// <summary>
    /// Mapování stávajících (legacy) proměnných prostředí na konfigurační klíče.
    /// Zachovává zpětnou kompatibilitu — tyto názvy mají nejvyšší prioritu.
    /// </summary>
    private static readonly (string EnvName, string ConfigKey)[] LegacyEnvMap =
    {
        ("CLAUDE_AGENT_WORKSPACE", $"{AgentSettings.SectionName}:{nameof(AgentSettings.Workspace)}"),
        ("CLAUDE_AGENT_MAX_ITERATIONS", $"{AgentSettings.SectionName}:{nameof(AgentSettings.MaxToolIterations)}"),
    };

    /// <summary>
    /// Načte konfiguraci ze všech vrstev v pořadí priorit (poslední vyhrává):
    /// appsettings.json → appsettings.local.json → proměnné prostředí
    /// (<c>ClaudeAgent__Klíč</c>) → legacy proměnné prostředí (<c>CLAUDE_AGENT_*</c>).
    /// </summary>
    /// <param name="basePath">Adresář, kde se hledají appsettings soubory.</param>
    /// <param name="envReader">Čtečka proměnných prostředí (injektovatelná kvůli testům).</param>
    public static AgentSettings Load(string basePath, Func<string, string?> envReader)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .AddInMemoryCollection(BuildLegacyOverrides(envReader))
            .Build();

        return Bind(config);
    }

    /// <summary>Naváže konfigurační sekci na <see cref="AgentSettings"/> (s výchozími hodnotami při absenci).</summary>
    internal static AgentSettings Bind(IConfiguration config) =>
        config.GetSection(AgentSettings.SectionName).Get<AgentSettings>() ?? new AgentSettings();

    /// <summary>Převede nastavené legacy proměnné prostředí na konfigurační klíče.</summary>
    internal static Dictionary<string, string?> BuildLegacyOverrides(Func<string, string?> envReader)
    {
        var overrides = new Dictionary<string, string?>();
        foreach (var (envName, configKey) in LegacyEnvMap)
        {
            var value = envReader(envName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                overrides[configKey] = value;
            }
        }

        return overrides;
    }
}

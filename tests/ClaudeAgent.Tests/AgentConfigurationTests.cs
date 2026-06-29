using ClaudeAgent;
using ClaudeAgent.Configuration;
using Microsoft.Extensions.Configuration;

namespace ClaudeAgent.Tests;

/// <summary>
/// Testy načítání konfigurace z appsettings.json, proměnných prostředí
/// a zpětně kompatibilních (legacy) proměnných prostředí.
/// </summary>
public class AgentConfigurationTests
{
  [Fact]
  public void Bind_BezSekce_VratiVychoziHodnoty()
  {
    var config = new ConfigurationBuilder().Build();

    var settings = AgentConfiguration.Bind(config);

    Assert.Equal(AgentLoop.DefaultModel, settings.Model);
    Assert.Equal(AgentLoop.DefaultMaxTokens, settings.MaxTokens);
    Assert.Equal(AgentLoop.DefaultMaxToolIterations, settings.MaxToolIterations);
    Assert.True(string.IsNullOrEmpty(settings.Workspace));
  }

  [Fact]
  public void Bind_NactePlnouSekci()
  {
    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
          ["ClaudeAgent:Model"] = "claude-test",
          ["ClaudeAgent:MaxTokens"] = "1234",
          ["ClaudeAgent:MaxToolIterations"] = "7",
          ["ClaudeAgent:Workspace"] = "C:\\ws",
        })
        .Build();

    var settings = AgentConfiguration.Bind(config);

    Assert.Equal("claude-test", settings.Model);
    Assert.Equal(1234, settings.MaxTokens);
    Assert.Equal(7, settings.MaxToolIterations);
    Assert.Equal("C:\\ws", settings.Workspace);
  }

  [Fact]
  public void BuildLegacyOverrides_PrevadiStavajiciEnvNaKlice()
  {
    var env = new Dictionary<string, string?>
    {
      ["CLAUDE_AGENT_WORKSPACE"] = "C:\\legacy",
      ["CLAUDE_AGENT_MAX_ITERATIONS"] = "9",
    };

    var result = AgentConfiguration.BuildLegacyOverrides(name => env.GetValueOrDefault(name));

    Assert.Equal("C:\\legacy", result["ClaudeAgent:Workspace"]);
    Assert.Equal("9", result["ClaudeAgent:MaxToolIterations"]);
  }

  [Fact]
  public void BuildLegacyOverrides_PrazdneEnv_VratiPrazdnouMapu()
  {
    var result = AgentConfiguration.BuildLegacyOverrides(_ => null);

    Assert.Empty(result);
  }

  [Fact]
  public void Load_JsonZaklad_LegacyEnvMaPrednost()
  {
    var dir = Path.Combine(Path.GetTempPath(), "claudeagent-cfg-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try
    {
      File.WriteAllText(Path.Combine(dir, "appsettings.json"),
          """
          {
            "ClaudeAgent": {
              "Model": "claude-from-json",
              "MaxTokens": 100,
              "MaxToolIterations": 5
            }
          }
          """);

      var env = new Dictionary<string, string?>
      {
        ["CLAUDE_AGENT_MAX_ITERATIONS"] = "42",
      };

      var settings = AgentConfiguration.Load(dir, name => env.GetValueOrDefault(name));

      Assert.Equal("claude-from-json", settings.Model);   // z appsettings.json
      Assert.Equal(100, settings.MaxTokens);               // z appsettings.json
      Assert.Equal(42, settings.MaxToolIterations);        // legacy env má přednost
    }
    finally
    {
      Directory.Delete(dir, recursive: true);
    }
  }
}

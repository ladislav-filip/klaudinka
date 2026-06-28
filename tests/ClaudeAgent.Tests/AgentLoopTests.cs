using System.Net;
using ClaudeAgent.Models;
using ClaudeAgent.Tests.Helpers;
using ClaudeAgent.Tools;

namespace ClaudeAgent.Tests;

/// <summary>
/// Testy agentní smyčky s mockovaným HTTP klientem.
/// </summary>
public class AgentLoopTests
{
  private const string SystemPrompt = "Test system prompt";

  [Fact]
  public async Task ProcessUserInputAsync_VratiTextPriEndTurn()
  {
    var handler = new MockHttpMessageHandler();
    handler.EnqueueResponse(HttpStatusCode.OK, ApiResponseFactory.EndTurn("Ahoj, jak mohu pomoci?"));

    using var httpClient = new HttpClient(handler);
    using var client = new AnthropicClient("test-api-key", httpClient);
    var root = Directory.GetCurrentDirectory();
    var registry = new ToolRegistry([new ReadFileTool(root), new WriteFileTool(root), new ListFilesTool(root)]);
    var loop = new AgentLoop(client, registry, SystemPrompt);

    var conversation = new List<Message>();
    var result = await loop.ProcessUserInputAsync(conversation, "Dobrý den");

    Assert.Equal("Ahoj, jak mohu pomoci?", result);
    Assert.Equal(2, conversation.Count);
  }

  [Fact]
  public async Task ProcessUserInputAsync_VykonaToolAPokracuje()
  {
    var handler = new MockHttpMessageHandler();
    handler.EnqueueResponse(HttpStatusCode.OK, ApiResponseFactory.ToolUse(
        "list_files", "toolu_01", new { path = "." }));
    handler.EnqueueResponse(HttpStatusCode.OK, ApiResponseFactory.EndTurn("Soubory byly vypsány."));

    using var httpClient = new HttpClient(handler);
    using var client = new AnthropicClient("test-api-key", httpClient);
    var root = Directory.GetCurrentDirectory();
    var registry = new ToolRegistry([new ReadFileTool(root), new WriteFileTool(root), new ListFilesTool(root)]);
    var loop = new AgentLoop(client, registry, SystemPrompt);

    var toolCalls = new List<string>();
    loop.ToolCallStarted += (_, e) => toolCalls.Add(e.ToolName);

    var conversation = new List<Message>();
    var result = await loop.ProcessUserInputAsync(conversation, "Vypiš soubory");

    Assert.Equal("Soubory byly vypsány.", result);
    Assert.Contains("list_files", toolCalls);
    Assert.Equal(4, conversation.Count);
    Assert.Equal(2, handler.Requests.Count);
  }

  [Fact]
  public void ExtractTextResponse_SpojujeTextoveBloky()
  {
    var content = new List<ContentBlock>
    {
      ContentBlock.TextBlock("První řádek"),
      ContentBlock.TextBlock("Druhý řádek")
    };

    var result = AgentLoop.ExtractTextResponse(content);

    Assert.Equal("První řádek\nDruhý řádek", result);
  }
}

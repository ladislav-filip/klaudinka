using System.Net;
using System.Text;
using System.Text.Json;
using ClaudeAgent.Models;

namespace ClaudeAgent.Tests.Helpers;

/// <summary>
/// Pomocná třída pro simulaci HTTP odpovědí Anthropic API v unit testech.
/// </summary>
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = new();

    public void EnqueueResponse(HttpStatusCode statusCode, string body) =>
        _responses.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });

    public void EnqueueResponse(Func<HttpRequestMessage, HttpResponseMessage> factory) =>
        _responses.Enqueue(factory);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("Žádná naplánovaná mock odpověď.");
        }

        return Task.FromResult(_responses.Dequeue()(request));
    }
}

/// <summary>
/// Factory metody pro sestavení typických API odpovědí v testech.
/// </summary>
internal static class ApiResponseFactory
{
    public static string EndTurn(string text) => JsonSerializer.Serialize(new
    {
        id = "msg_test",
        type = "message",
        role = "assistant",
        content = new[] { new { type = "text", text } },
        model = "claude-sonnet-4-6",
        stop_reason = "end_turn",
        usage = new { input_tokens = 10, output_tokens = 20 }
    });

    public static string ToolUse(string toolName, string toolUseId, object input) => JsonSerializer.Serialize(new
    {
        id = "msg_test",
        type = "message",
        role = "assistant",
        content = new[]
        {
            new
            {
                type = "tool_use",
                id = toolUseId,
                name = toolName,
                input
            }
        },
        model = "claude-sonnet-4-6",
        stop_reason = "tool_use",
        usage = new { input_tokens = 10, output_tokens = 20 }
    });

    /// <summary>
    /// Odpověď se stop_reason "tool_use", ale bez jediného tool_use bloku — simuluje
    /// nekonzistentní stav, který by jinak vedl k zacyklení smyčky.
    /// </summary>
    public static string ToolUseNoBlocks() => JsonSerializer.Serialize(new
    {
        id = "msg_test",
        type = "message",
        role = "assistant",
        content = Array.Empty<object>(),
        model = "claude-sonnet-4-6",
        stop_reason = "tool_use",
        usage = new { input_tokens = 10, output_tokens = 20 }
    });
}

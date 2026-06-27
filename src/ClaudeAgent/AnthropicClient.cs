using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeAgent.Models;

namespace ClaudeAgent;

/// <summary>
/// HTTP klient pro přímé volání Anthropic Messages API bez externího SDK.
/// </summary>
public sealed class AnthropicClient : IDisposable
{
    private const string ApiEndpoint = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new MessageJsonConverter() }
    };

    public AnthropicClient(string apiKey, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API klíč nesmí být prázdný.", nameof(apiKey));
        }

        _apiKey = apiKey;
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>Odešle požadavek na Messages API a vrátí deserializovanou odpověď.</summary>
    public async Task<ApiResponse> SendAsync(ApiRequest request, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        httpRequest.Headers.Add("x-api-key", _apiKey);
        httpRequest.Headers.Add("anthropic-version", ApiVersion);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Anthropic API vrátilo {(int)response.StatusCode}: {responseBody}");
        }

        var apiResponse = JsonSerializer.Deserialize<ApiResponse>(responseBody, JsonOptions)
            ?? throw new InvalidOperationException("Nepodařilo se deserializovat odpověď API.");

        return apiResponse;
    }

    public void Dispose() => _httpClient.Dispose();
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Storymark.Api.Domain.Llm;

namespace Storymark.Api.Infrastructure.Llm;

/// <summary>Talks to any OpenAI-compatible chat/embeddings API. The base URL and
/// API key come from the resolved model's provider (ADR-0016), passed per call.</summary>
internal sealed class OpenAiCompatibleClient(HttpClient http) : IChatModelClient
{
    public LlmProviderKind Kind => LlmProviderKind.OpenAiCompatible;

    public async Task<ChatResponse> CompleteAsync(ChatRequest request, LlmEndpoint endpoint, CancellationToken ct)
    {
        var payload = new ChatPayload
        {
            Model = request.Model,
            Temperature = request.Temperature,
            Messages = WireChatMessage.From(request.Messages),
        };

        using var response = await HttpResilience.SendWithRetryAsync(
            token => http.SendAsync(BuildRequest(endpoint, "chat/completions", payload), token), ct);
        await EnsureSuccessAsync(response, ct);
        var body = await response.Content.ReadFromJsonAsync<ChatBody>(ct)
            ?? throw new InvalidOperationException("OpenAI-compatible API returned an empty chat response.");

        var choice = body.Choices?.FirstOrDefault()
            ?? throw new InvalidOperationException("OpenAI-compatible chat response contained no choices.");

        return new ChatResponse
        {
            Content = choice.Message?.Content ?? string.Empty,
            TokensIn = body.Usage?.PromptTokens,
            TokensOut = body.Usage?.CompletionTokens,
        };
    }

    public async Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, LlmEndpoint endpoint, CancellationToken ct)
    {
        var payload = new WireEmbedRequest { Model = request.Model, Input = request.Input };

        using var response = await HttpResilience.SendWithRetryAsync(
            token => http.SendAsync(BuildRequest(endpoint, "embeddings", payload), token), ct);
        await EnsureSuccessAsync(response, ct);
        var body = await response.Content.ReadFromJsonAsync<EmbedBody>(ct)
            ?? throw new InvalidOperationException("OpenAI-compatible API returned an empty embedding response.");

        var data = body.Data?.FirstOrDefault()
            ?? throw new InvalidOperationException("OpenAI-compatible embedding response contained no data.");

        return new EmbeddingResponse
        {
            Embedding = data.Embedding
                ?? throw new InvalidOperationException("OpenAI-compatible embedding vector was null."),
            Tokens = body.Usage?.PromptTokens,
        };
    }

    // EnsureSuccessStatusCode discards the response body, but OpenAI-compatible
    // providers put the actionable reason there ("Incorrect API key provided",
    // "model not found"). Surface it so the recorded failure is diagnosable.
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var detail = string.IsNullOrWhiteSpace(body) ? string.Empty : $": {body.Trim()}";
        throw new HttpRequestException(
            $"OpenAI-compatible API returned {(int)response.StatusCode} {response.ReasonPhrase}{detail}");
    }

    private static HttpRequestMessage BuildRequest<TPayload>(LlmEndpoint endpoint, string path, TPayload payload)
    {
        if (string.IsNullOrWhiteSpace(endpoint.BaseUrl))
        {
            throw new InvalidOperationException("The provider has no base URL configured.");
        }

        if (string.IsNullOrWhiteSpace(endpoint.ApiKey))
        {
            throw new InvalidOperationException("The provider has no API key configured.");
        }

        return new HttpRequestMessage(HttpMethod.Post, endpoint.ResolveUri(path))
        {
            Content = JsonContent.Create(payload),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", endpoint.ApiKey) },
        };
    }

    private sealed record ChatPayload
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("messages")]
        public required IReadOnlyList<WireChatMessage> Messages { get; init; }

        [JsonPropertyName("temperature")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Temperature { get; init; }
    }

    private sealed record ChatBody
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; init; }

        [JsonPropertyName("usage")]
        public UsageBody? Usage { get; init; }
    }

    private sealed record Choice
    {
        [JsonPropertyName("message")]
        public WireChatMessage? Message { get; init; }
    }

    private sealed record UsageBody
    {
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; init; }

        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokens { get; init; }
    }

    private sealed record EmbedBody
    {
        [JsonPropertyName("data")]
        public List<EmbedData>? Data { get; init; }

        [JsonPropertyName("usage")]
        public UsageBody? Usage { get; init; }
    }

    private sealed record EmbedData
    {
        [JsonPropertyName("embedding")]
        public IReadOnlyList<float>? Embedding { get; init; }
    }
}

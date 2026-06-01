using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Infrastructure.Llm;

/// <summary>Talks to any OpenAI-compatible chat/embeddings API. Base URL
/// (<c>llm.openai.baseUrl</c>) and API key (<c>llm.openai.apiKey</c>, a secret)
/// are read from settings per call, so they rotate without a restart.</summary>
internal sealed class OpenAiCompatibleClient(HttpClient http, ISettingsService settings) : IChatModelClient
{
    public string ProviderName => "openai_compatible";

    public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        var payload = new ChatPayload
        {
            Model = request.Model,
            Temperature = request.Temperature,
            Messages = WireChatMessage.From(request.Messages),
        };

        using var message = BuildRequest("chat/completions", payload);
        using var response = await http.SendAsync(message, ct);
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

    public async Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken ct)
    {
        var payload = new WireEmbedRequest { Model = request.Model, Input = request.Input };

        using var message = BuildRequest("embeddings", payload);
        using var response = await http.SendAsync(message, ct);
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

    private HttpRequestMessage BuildRequest<TPayload>(string path, TPayload payload)
    {
        var baseUrl = settings.GetString("llm.openai.baseUrl");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Setting 'llm.openai.baseUrl' is not configured.");
        }

        var apiKey = settings.GetString("llm.openai.apiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Setting 'llm.openai.apiKey' is not configured.");
        }

        var uri = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), path);
        return new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", apiKey) },
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

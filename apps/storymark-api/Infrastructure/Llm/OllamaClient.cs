using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Storymark.Api.Domain.Llm;

namespace Storymark.Api.Infrastructure.Llm;

/// <summary>Talks to a local Ollama server (base address from the resolved provider).
/// Chat via <c>/api/chat</c>, embeddings via <c>/api/embed</c>, both non-streaming.</summary>
internal sealed class OllamaClient(HttpClient http) : IChatModelClient
{
    public LlmProviderKind Kind => LlmProviderKind.Ollama;

    public async Task<ChatResponse> CompleteAsync(ChatRequest request, LlmEndpoint endpoint, CancellationToken ct)
    {
        var payload = new ChatPayload
        {
            Model = request.Model,
            Stream = false,
            Messages = WireChatMessage.From(request.Messages),
        };

        using var response = await HttpResilience.SendWithRetryAsync(
            token => http.PostAsJsonAsync(endpoint.ResolveUri("api/chat"), payload, token), ct);
        ThrowIfModelMissing(response, request.Model);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ChatBody>(ct)
            ?? throw new InvalidOperationException("Ollama returned an empty chat response.");

        return new ChatResponse
        {
            Content = body.Message?.Content ?? string.Empty,
            TokensIn = body.PromptEvalCount,
            TokensOut = body.EvalCount,
        };
    }

    public async Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, LlmEndpoint endpoint, CancellationToken ct)
    {
        var payload = new WireEmbedRequest { Model = request.Model, Input = request.Input };

        using var response = await HttpResilience.SendWithRetryAsync(
            token => http.PostAsJsonAsync(endpoint.ResolveUri("api/embed"), payload, token), ct);
        ThrowIfModelMissing(response, request.Model);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<EmbedBody>(ct)
            ?? throw new InvalidOperationException("Ollama returned an empty embedding response.");

        var embedding = body.Embeddings?.FirstOrDefault()
            ?? throw new InvalidOperationException("Ollama embedding response contained no vectors.");

        return new EmbeddingResponse { Embedding = embedding, Tokens = body.PromptEvalCount };
    }

    // Ollama's /api/chat and /api/embed routes exist in every supported version, so
    // a 404 from them means the requested model has not been pulled — surfaced as a
    // typed, non-transient failure rather than a generic 5xx-style HttpRequestException.
    private static void ThrowIfModelMissing(HttpResponseMessage response, string model)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ModelNotFoundException("ollama", model);
        }
    }

    private sealed record ChatPayload
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("messages")]
        public required IReadOnlyList<WireChatMessage> Messages { get; init; }

        [JsonPropertyName("stream")]
        public required bool Stream { get; init; }
    }

    private sealed record ChatBody
    {
        [JsonPropertyName("message")]
        public WireChatMessage? Message { get; init; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; init; }

        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; init; }
    }

    private sealed record EmbedBody
    {
        [JsonPropertyName("embeddings")]
        public List<List<float>>? Embeddings { get; init; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; init; }
    }
}

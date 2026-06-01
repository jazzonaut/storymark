using System.Text.Json;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Llm;

namespace Storymark.Api.Services.Llm;

/// <summary>Resolves a task (or the embedding setting) to its model via the
/// <see cref="LlmRegistryService"/> — read live, so a reassignment takes effect on
/// the next call without a restart — then runs the call through
/// <see cref="LlmCallRecorder"/>. The only entry point other services use for chat
/// and embeddings. See docs/plan.md §20 and ADR-0016.</summary>
internal sealed class ChatModelRouter
{
    private readonly Dictionary<LlmProviderKind, IChatModelClient> _clients;
    private readonly LlmRegistryService _registry;
    private readonly LlmCallRecorder _recorder;

    public ChatModelRouter(
        IEnumerable<IChatModelClient> clients,
        LlmRegistryService registry,
        LlmCallRecorder recorder)
    {
        _clients = clients.ToDictionary(c => c.Kind);
        _registry = registry;
        _recorder = recorder;
    }

    public async Task<ChatCompletion> CompleteAsync(
        string task,
        IReadOnlyList<ChatMessage> messages,
        LlmCallMetadata metadata,
        CancellationToken ct)
    {
        var model = await _registry.ResolveTaskAsync(task, ct);
        var client = ClientFor(model.Kind);
        var request = new ChatRequest { Model = model.ModelName, Messages = messages };
        var endpoint = EndpointFor(model);

        var descriptor = Describe(task, model, SerializePrompt(messages), metadata);

        var response = await _recorder.RecordAsync(
            descriptor,
            async token =>
            {
                var chat = await client.CompleteAsync(request, endpoint, token);
                return new LlmCallResult<ChatResponse>
                {
                    Value = chat,
                    ResponseText = chat.Content,
                    TokensIn = chat.TokensIn,
                    TokensOut = chat.TokensOut,
                };
            },
            ct);

        return new ChatCompletion
        {
            Provider = model.ProviderName,
            Model = model.ModelName,
            Content = response.Content,
            TokensIn = response.TokensIn,
            TokensOut = response.TokensOut,
        };
    }

    public async Task<IReadOnlyList<float>> EmbedAsync(
        string text,
        LlmCallMetadata metadata,
        CancellationToken ct)
    {
        var model = await _registry.ResolveEmbeddingAsync(ct);
        var client = ClientFor(model.Kind);
        var request = new EmbeddingRequest { Model = model.ModelName, Input = text };
        var endpoint = EndpointFor(model);

        var descriptor = Describe("embed", model, text, metadata);

        var response = await _recorder.RecordAsync(
            descriptor,
            async token =>
            {
                var embedding = await client.EmbedAsync(request, endpoint, token);
                return new LlmCallResult<EmbeddingResponse>
                {
                    Value = embedding,
                    ResponseText = null,
                    TokensIn = embedding.Tokens,
                    TokensOut = null,
                };
            },
            ct);

        return response.Embedding;
    }

    /// <summary>The provider kind a task currently resolves to, without making a
    /// call — lets a caller branch on local vs hosted (e.g. to size the prompt).</summary>
    public async Task<LlmProviderKind> ResolveTaskKindAsync(string task, CancellationToken ct) =>
        (await _registry.ResolveTaskAsync(task, ct)).Kind;

    private static LlmCallDescriptor Describe(
        string task, ResolvedModel model, string prompt, LlmCallMetadata metadata) =>
        new()
        {
            Task = task,
            Provider = model.ProviderName,
            Model = model.ModelName,
            Prompt = prompt,
            BookId = metadata.BookId,
            SectionIndex = metadata.SectionIndex,
            UsedFutureContent = metadata.UsedFutureContent,
            InputPerMillion = model.InputPerMillion,
            OutputPerMillion = model.OutputPerMillion,
        };

    private static LlmEndpoint EndpointFor(ResolvedModel model) =>
        new() { BaseUrl = model.BaseUrl, ApiKey = model.ApiKey };

    private IChatModelClient ClientFor(LlmProviderKind kind)
    {
        if (!_clients.TryGetValue(kind, out var client))
        {
            throw new InvalidOperationException($"No chat model client is registered for kind '{kind}'.");
        }

        return client;
    }

    private static string SerializePrompt(IReadOnlyList<ChatMessage> messages)
        => JsonSerializer.Serialize(WireChatMessage.From(messages));
}

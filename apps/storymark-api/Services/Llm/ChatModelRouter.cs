using System.Text.Json;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Llm;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Services.Llm;

/// <summary>Resolves a task to its configured <c>provider:model</c> from settings
/// — read live, so a model swap takes effect on the next call without a restart —
/// then runs the call through <see cref="LlmCallRecorder"/>. The only entry point
/// other services use for chat and embeddings. See docs/plan.md §20.</summary>
internal sealed class ChatModelRouter
{
    private readonly Dictionary<string, IChatModelClient> _clients;
    private readonly ISettingsService _settings;
    private readonly LlmCallRecorder _recorder;

    public ChatModelRouter(
        IEnumerable<IChatModelClient> clients,
        ISettingsService settings,
        LlmCallRecorder recorder)
    {
        _clients = clients.ToDictionary(c => c.ProviderName, StringComparer.Ordinal);
        _settings = settings;
        _recorder = recorder;
    }

    public async Task<ChatCompletion> CompleteAsync(
        string task,
        IReadOnlyList<ChatMessage> messages,
        LlmCallMetadata metadata,
        CancellationToken ct)
    {
        var (provider, model) = Resolve($"llm.chat.taskMap.{task}");
        var client = ClientFor(provider);
        var request = new ChatRequest { Model = model, Messages = messages };

        var descriptor = new LlmCallDescriptor
        {
            Task = task,
            Provider = provider,
            Model = model,
            Prompt = SerializePrompt(messages),
            BookId = metadata.BookId,
            SectionIndex = metadata.SectionIndex,
            UsedFutureContent = metadata.UsedFutureContent,
        };

        var response = await _recorder.RecordAsync(
            descriptor,
            async token =>
            {
                var chat = await client.CompleteAsync(request, token);
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
            Provider = provider,
            Model = model,
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
        var (provider, model) = Resolve("llm.embedding.modelId");
        var client = ClientFor(provider);
        var request = new EmbeddingRequest { Model = model, Input = text };

        var descriptor = new LlmCallDescriptor
        {
            Task = "embed",
            Provider = provider,
            Model = model,
            Prompt = text,
            BookId = metadata.BookId,
            SectionIndex = metadata.SectionIndex,
            UsedFutureContent = metadata.UsedFutureContent,
        };

        var response = await _recorder.RecordAsync(
            descriptor,
            async token =>
            {
                var embedding = await client.EmbedAsync(request, token);
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

    /// <summary>Resolves a chat task to its configured <c>(provider, model)</c>
    /// without making a call, so a caller can branch on where the task runs (local
    /// vs hosted) before building the request — e.g. to size the prompt.</summary>
    public (string Provider, string Model) ResolveTask(string task) =>
        Resolve($"llm.chat.taskMap.{task}");

    private (string Provider, string Model) Resolve(string settingKey)
    {
        var value = _settings.GetString(settingKey);
        var separator = value.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0 || separator == value.Length - 1)
        {
            throw new InvalidOperationException(
                $"Setting '{settingKey}' must be 'provider:model'; got '{value}'.");
        }

        return (value[..separator], value[(separator + 1)..]);
    }

    private IChatModelClient ClientFor(string provider)
    {
        if (!_clients.TryGetValue(provider, out var client))
        {
            throw new InvalidOperationException(
                $"No chat model client is registered for provider '{provider}'.");
        }

        return client;
    }

    private static string SerializePrompt(IReadOnlyList<ChatMessage> messages)
        => JsonSerializer.Serialize(WireChatMessage.From(messages));
}

namespace Storymark.Api.Infrastructure.Llm;

/// <summary>A provider-specific chat/embedding backend. Resolved by
/// <c>ProviderName</c> from the <c>provider:model</c> routing string. See
/// docs/plan.md §20.1.</summary>
internal interface IChatModelClient
{
    string ProviderName { get; }

    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct);

    Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, CancellationToken ct);
}

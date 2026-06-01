using Storymark.Api.Domain.Llm;

namespace Storymark.Api.Infrastructure.Llm;

/// <summary>A provider-specific chat/embedding backend, selected by <see cref="Kind"/>
/// from the resolved model's provider. The endpoint (base URL + key) is passed per
/// call rather than fixed at construction, so one client serves many providers of
/// the same kind. See ADR-0016.</summary>
internal interface IChatModelClient
{
    LlmProviderKind Kind { get; }

    Task<ChatResponse> CompleteAsync(ChatRequest request, LlmEndpoint endpoint, CancellationToken ct);

    Task<EmbeddingResponse> EmbedAsync(EmbeddingRequest request, LlmEndpoint endpoint, CancellationToken ct);
}

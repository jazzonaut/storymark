namespace Storymark.Api.Infrastructure.Llm;

internal sealed record EmbeddingResponse
{
    public required IReadOnlyList<float> Embedding { get; init; }

    public int? Tokens { get; init; }
}

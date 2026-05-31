namespace Storymark.Api.Infrastructure.Llm;

internal sealed record EmbeddingRequest
{
    public required string Model { get; init; }

    public required string Input { get; init; }
}

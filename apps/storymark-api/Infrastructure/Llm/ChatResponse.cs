namespace Storymark.Api.Infrastructure.Llm;

internal sealed record ChatResponse
{
    public required string Content { get; init; }

    public int? TokensIn { get; init; }

    public int? TokensOut { get; init; }
}

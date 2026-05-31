namespace Storymark.Api.Domain.Llm;

internal sealed record ChatMessage
{
    public required ChatRole Role { get; init; }

    public required string Content { get; init; }
}

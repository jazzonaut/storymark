using Storymark.Api.Domain.Llm;

namespace Storymark.Api.Infrastructure.Llm;

internal sealed record ChatRequest
{
    public required string Model { get; init; }

    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    public double? Temperature { get; init; }
}

namespace Storymark.Api.Infrastructure.Llm;

/// <summary>Everything <see cref="LlmCallRecorder"/> needs to persist a call,
/// resolved by the router before the call is made.</summary>
internal sealed record LlmCallDescriptor
{
    public required string Task { get; init; }

    public required string Provider { get; init; }

    public required string Model { get; init; }

    public required string Prompt { get; init; }

    public Guid? BookId { get; init; }

    public int? SectionIndex { get; init; }

    public bool UsedFutureContent { get; init; }
}

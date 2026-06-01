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

    /// <summary>Per-million-token pricing of the resolved model (ADR-0016), used by
    /// <see cref="LlmCallRecorder"/> to estimate cost. Null for an unpriced model.</summary>
    public decimal? InputPerMillion { get; init; }

    public decimal? OutputPerMillion { get; init; }
}

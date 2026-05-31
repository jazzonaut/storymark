namespace Storymark.Api.Services.Retrieval;

/// <summary>One spoiler-safe evidence chunk returned by
/// <see cref="SpoilerSafeRetrievalService"/>, already reranked and de-duplicated.
/// <see cref="LocationStart"/>/<see cref="LocationEnd"/> are offsets into the
/// section's plain text (the shared coordinate space). <see cref="Score"/> is the
/// combined normalised relevance across the semantic and keyword passes (higher is
/// better); it is not comparable across different queries.</summary>
internal sealed record EvidenceChunk
{
    public required Guid Id { get; init; }

    public required int SectionIndex { get; init; }

    public required int ChunkIndex { get; init; }

    public required int LocationStart { get; init; }

    public required int LocationEnd { get; init; }

    public required string Text { get; init; }

    public required double Score { get; init; }
}

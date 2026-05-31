namespace Storymark.Api.Services.Books;

/// <summary>A planned chunk produced by <see cref="SectionChunkingService"/>: the
/// merged paragraph text plus its half-open offsets into the section's plain-text
/// rendering (the same coordinate space as <see cref="Domain.Books.BookParagraph"/>).
/// The job turns each draft into a <see cref="Domain.Books.BookChunk"/> row.</summary>
internal sealed record ChunkDraft
{
    public required int LocationStart { get; init; }

    public required int LocationEnd { get; init; }

    public required string Text { get; init; }

    public required int TokenEstimate { get; init; }
}

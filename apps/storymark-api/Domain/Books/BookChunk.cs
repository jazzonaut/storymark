using Pgvector;

namespace Storymark.Api.Domain.Books;

/// <summary>A retrieval unit: a paragraph-aware merge of one or more consecutive
/// <see cref="BookParagraph"/> blocks within a section (docs/plan.md §12.4, §15.3).
/// <see cref="LocationStart"/>/<see cref="LocationEnd"/> are character offsets into
/// the section's plain-text rendering — the same coordinate space as
/// <see cref="BookParagraph"/> — so a chunk's spoiler position is comparable to a
/// reader's. Consecutive chunks overlap by 100–150 words, so their offset ranges
/// overlap too.
///
/// <para><see cref="Embedding"/> and its model tags are written by the S3.2
/// embedding job; they are null until then.</para></summary>
internal sealed class BookChunk
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    public Guid SectionId { get; set; }

    public int SectionIndex { get; set; }

    /// <summary>0-based position within the section's chunk sequence.</summary>
    public int ChunkIndex { get; set; }

    public int LocationStart { get; set; }

    public int LocationEnd { get; set; }

    public required string Text { get; set; }

    /// <summary>Rough token count for batching and cost estimation; not exact.</summary>
    public int TokenEstimate { get; set; }

    /// <summary>Set by the embedding job (S3.2); search only considers chunks whose
    /// <see cref="EmbeddingModelId"/> matches the currently-selected model.</summary>
    public Vector? Embedding { get; set; }

    public string? EmbeddingModelId { get; set; }

    public string? EmbeddingModelVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

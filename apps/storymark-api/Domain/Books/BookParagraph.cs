namespace Storymark.Api.Domain.Books;

/// <summary>A paragraph-level block within a section (docs/plan.md §12.3).
/// <see cref="LocationStart"/>/<see cref="LocationEnd"/> are character offsets into
/// the section's plain-text rendering (the <c>NNNN.txt</c> artifact): a half-open
/// range <c>[start, end)</c> such that the section text's
/// <c>Substring(start, end - start)</c> is exactly <see cref="PlainText"/>. The
/// separator between consecutive paragraphs belongs to no paragraph's range. This
/// is the shared coordinate space for chunking, retrieval, and reader position.</summary>
internal sealed class BookParagraph
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    public Guid SectionId { get; set; }

    public int SectionIndex { get; set; }

    public int ParagraphIndex { get; set; }

    public int LocationStart { get; set; }

    public int LocationEnd { get; set; }

    /// <summary>Sanitised HTML of this paragraph block, as the reader renders it.</summary>
    public string? Html { get; set; }

    public required string PlainText { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

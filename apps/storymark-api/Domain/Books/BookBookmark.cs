namespace Storymark.Api.Domain.Books;

/// <summary>A reader-saved position in a book (docs/plan.md §12.6). The
/// <see cref="Id"/> is supplied by the client so create/delete are idempotent
/// across the offline queue's replays.</summary>
internal sealed class BookBookmark
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    public int SectionIndex { get; set; }

    public int? ParagraphIndex { get; set; }

    public int LocationStart { get; set; }

    /// <summary>Optional short label (a preview of the bookmarked text).</summary>
    public string? Label { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

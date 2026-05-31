namespace Storymark.Api.Domain.Books;

/// <summary>A single reading-order unit of a parsed book (an EPUB spine item),
/// holding the on-disk paths to its sanitised HTML and plain-text renderings
/// (docs/plan.md §12.2). The plain text at <see cref="TextPath"/> is the canonical
/// coordinate space that <see cref="BookParagraph.LocationStart"/>/<c>LocationEnd</c>
/// index into.</summary>
internal sealed class BookSection
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    /// <summary>1-based position in the reading order (spine).</summary>
    public int SectionIndex { get; set; }

    public string? Title { get; set; }

    /// <summary>The EPUB-internal file path this section came from.</summary>
    public string? SourceHref { get; set; }

    public string? HtmlPath { get; set; }

    public string? TextPath { get; set; }

    /// <summary>SHA-256 of the plain-text rendering; lets later reprocessing
    /// detect an unchanged section.</summary>
    public string? TextHash { get; set; }

    public int WordCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

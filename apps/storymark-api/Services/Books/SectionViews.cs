namespace Storymark.Api.Services.Books;

/// <summary>One entry in the section manifest (<c>GET /api/books/{id}/sections</c>).</summary>
internal sealed record SectionSummaryView
{
    public required int SectionIndex { get; init; }

    public string? Title { get; init; }

    public required int WordCount { get; init; }

    public required int ParagraphCount { get; init; }
}

/// <summary>A paragraph within a section, with its offsets into the section's
/// plain text (see <see cref="Domain.Books.BookParagraph"/>).</summary>
internal sealed record ParagraphView
{
    public required int ParagraphIndex { get; init; }

    public required int LocationStart { get; init; }

    public required int LocationEnd { get; init; }

    public string? Html { get; init; }

    public required string Text { get; init; }
}

/// <summary>Full section content (<c>GET /api/books/{id}/sections/{idx}</c>):
/// sanitised HTML plus paragraph offsets.</summary>
internal sealed record SectionContentView
{
    public required int SectionIndex { get; init; }

    public string? Title { get; init; }

    public required string Html { get; init; }

    public required IReadOnlyList<ParagraphView> Paragraphs { get; init; }
}

/// <summary>A resolved inline resource file ready to serve.</summary>
internal sealed record BookResourceView
{
    public required string FilePath { get; init; }

    public required string ContentType { get; init; }
}

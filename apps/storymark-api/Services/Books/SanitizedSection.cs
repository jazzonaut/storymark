namespace Storymark.Api.Services.Books;

/// <summary>A paragraph block after sanitisation, with its offsets into the
/// section plain text (see <see cref="Domain.Books.BookParagraph"/>).</summary>
internal sealed record SanitizedParagraph
{
    public required string Html { get; init; }

    public required string PlainText { get; init; }

    public required int LocationStart { get; init; }

    public required int LocationEnd { get; init; }
}

/// <summary>The result of sanitising one section's XHTML: the full sanitised HTML
/// (written to <c>NNNN.xhtml</c> and served by the reader), the plain-text
/// rendering (written to <c>NNNN.txt</c> and the coordinate space for offsets),
/// the paragraph blocks, and the set of inline image paths actually referenced
/// (so the job stores only those).</summary>
internal sealed record SanitizedSection
{
    public required string SanitizedHtml { get; init; }

    public required string PlainText { get; init; }

    public required int WordCount { get; init; }

    public required IReadOnlyList<SanitizedParagraph> Paragraphs { get; init; }

    public required IReadOnlySet<string> UsedImagePaths { get; init; }
}

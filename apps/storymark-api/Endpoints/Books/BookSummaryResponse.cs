using Storymark.Api.Domain.Books;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>Library list/upload response. Mirrors the <c>BookSummary</c> contract
/// in @storymark/shared-types. <c>lastReadSection</c>/<c>lastOpenedAt</c> are
/// reading-progress fields (Phase 3) and are null until then.</summary>
internal sealed record BookSummaryResponse
{
    public required Guid BookId { get; init; }

    public required string Title { get; init; }

    public string? Author { get; init; }

    public required string Format { get; init; }

    public required string CoverUrl { get; init; }

    public required string ProcessingStatus { get; init; }

    /// <summary>Why the last processing attempt failed, or null. Set even while a
    /// transient failure is mid-retry, so a stalled book explains itself.</summary>
    public string? ProcessingError { get; init; }

    /// <summary>Embedding progress while the book is on <c>Embedding</c>, else null.</summary>
    public EmbeddingProgressResponse? EmbeddingProgress { get; init; }

    /// <summary>Extraction progress while the book is on <c>ExtractingPerSection</c>, else null.</summary>
    public ExtractionProgressResponse? ExtractionProgress { get; init; }

    public int? LastReadSection { get; init; }

    public DateTimeOffset? LastOpenedAt { get; init; }

    /// <summary>When the book's derived data was last rewritten (extraction,
    /// embedding, or memory snapshots). The client stores it as a version token on
    /// its cached memory surfaces, so a reprocess invalidates them.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    // A freshly imported book has no chunks or sections yet (status Uploaded), so it
    // has no progress of either kind — map it through an empty entry.
    public static BookSummaryResponse From(Book book) =>
        From(new BookCatalogEntry
        {
            Book = book,
            EmbeddedChunks = 0,
            TotalChunks = 0,
            ExtractedSections = 0,
            TotalSections = 0,
        });

    public static BookSummaryResponse From(BookCatalogEntry entry)
    {
        var book = entry.Book;
        return new BookSummaryResponse
        {
            BookId = book.Id,
            Title = book.Title,
            Author = book.Author,
            Format = book.Format,
            CoverUrl = $"/api/books/{book.Id}/cover",
            ProcessingStatus = book.ProcessingStatus.ToString(),
            ProcessingError = book.ProcessingError,
            UpdatedAt = book.UpdatedAt,
            EmbeddingProgress =
                book.ProcessingStatus == Domain.Books.ProcessingStatus.Embedding && entry.TotalChunks > 0
                    ? new EmbeddingProgressResponse { Embedded = entry.EmbeddedChunks, Total = entry.TotalChunks }
                    : null,
            ExtractionProgress =
                book.ProcessingStatus == Domain.Books.ProcessingStatus.ExtractingPerSection && entry.TotalSections > 0
                    ? new ExtractionProgressResponse { Extracted = entry.ExtractedSections, Total = entry.TotalSections }
                    : null,
        };
    }
}

/// <summary>Chunks vectorised under the current model / total chunks. Mirrors
/// <c>EmbeddingProgress</c> in @storymark/shared-types.</summary>
internal sealed record EmbeddingProgressResponse
{
    public required int Embedded { get; init; }

    public required int Total { get; init; }
}

/// <summary>Section summaries written / total sections. Mirrors
/// <c>ExtractionProgress</c> in @storymark/shared-types.</summary>
internal sealed record ExtractionProgressResponse
{
    public required int Extracted { get; init; }

    public required int Total { get; init; }
}

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

    public int? LastReadSection { get; init; }

    public DateTimeOffset? LastOpenedAt { get; init; }

    // A freshly imported book has no chunks yet (status Uploaded), so it has no
    // embedding progress — map it through an empty entry.
    public static BookSummaryResponse From(Book book) =>
        From(new BookCatalogEntry { Book = book, EmbeddedChunks = 0, TotalChunks = 0 });

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
            EmbeddingProgress =
                book.ProcessingStatus == Domain.Books.ProcessingStatus.Embedding && entry.TotalChunks > 0
                    ? new EmbeddingProgressResponse { Embedded = entry.EmbeddedChunks, Total = entry.TotalChunks }
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

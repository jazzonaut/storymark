using Storymark.Api.Domain.Books;

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

    public int? LastReadSection { get; init; }

    public DateTimeOffset? LastOpenedAt { get; init; }

    public static BookSummaryResponse From(Book book) =>
        new()
        {
            BookId = book.Id,
            Title = book.Title,
            Author = book.Author,
            Format = book.Format,
            CoverUrl = $"/api/books/{book.Id}/cover",
            ProcessingStatus = book.ProcessingStatus.ToString(),
        };
}

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Services.Llm;

namespace Storymark.Api.Services.Books;

/// <summary>The result of <see cref="BookCatalogService.SetVisibilityAsync"/>: an
/// outcome plus the updated book when it succeeded.</summary>
internal sealed record SetVisibilityResult
{
    public required OwnedBookOutcome Outcome { get; init; }

    public Book? Book { get; init; }
}

/// <summary>Read access to the book catalogue for the library list and cover
/// endpoint, plus the owner-gated visibility flip. Aggregation/queries live here,
/// not in the endpoints (layering).</summary>
internal sealed class BookCatalogService(StorymarkDbContext db, LlmRegistryService registry)
{
    public async Task<IReadOnlyList<BookCatalogEntry>> GetAllAsync(Guid userId, CancellationToken ct)
    {
        var books = await db.Books
            .AsNoTracking()
            .VisibleTo(userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

        // The aggregates are constrained to the visible books being returned so a
        // reader's library request never pays to scan other accounts' private books.
        var bookIds = books.Select(b => b.Id).ToList();

        var latestProgressByBook = await GetLatestProgressByBookAsync(userId, bookIds, ct);

        // Embedding progress is chunks already vectorised under the *current* model
        // over the book's total chunks — the same "current model only" filter search
        // uses (docs/plan.md §15.5), so a mid-reindex book counts honestly. Two plain
        // grouped counts rather than one filtered aggregate, to keep the SQL trivially
        // translatable.
        var modelId = await registry.ResolveEmbeddingIdentityAsync(ct);
        var totalByBook = await CountByBookAsync(
            db.BookChunks.Where(c => bookIds.Contains(c.BookId)), c => c.BookId, ct);
        var embeddedByBook = await CountByBookAsync(
            db.BookChunks.Where(c => bookIds.Contains(c.BookId)
                && c.Embedding != null && c.EmbeddingModelId == modelId),
            c => c.BookId,
            ct);

        // Extraction progress is section summaries written / total sections — the job
        // writes one summary per processed section (SectionExtractionJob), so it is as
        // honest a fraction as embedding's chunk count. Skipped sections never produce a
        // summary, so a book with skips tops out just shy of total; that is correct (the
        // badge clears on the status transition, not on reaching the denominator).
        var totalSectionsByBook = await CountByBookAsync(
            db.BookSections.Where(s => bookIds.Contains(s.BookId)), s => s.BookId, ct);
        var extractedByBook = await CountByBookAsync(
            db.SectionSummaries.Where(s => bookIds.Contains(s.BookId)), s => s.BookId, ct);

        // Library order is "last activity": last read when the reader has opened the
        // book, upload time otherwise — so a fresh upload starts on top and drifts
        // down as other books are read.
        return books
            .Select(b =>
            {
                var progress = latestProgressByBook.GetValueOrDefault(b.Id);
                return new BookCatalogEntry
                {
                    Book = b,
                    TotalChunks = totalByBook.GetValueOrDefault(b.Id),
                    EmbeddedChunks = embeddedByBook.GetValueOrDefault(b.Id),
                    TotalSections = totalSectionsByBook.GetValueOrDefault(b.Id),
                    ExtractedSections = extractedByBook.GetValueOrDefault(b.Id),
                    LastReadSection = progress?.SectionIndex,
                    LastOpenedAt = progress?.UpdatedAt,
                };
            })
            .OrderByDescending(e => e.LastOpenedAt ?? e.Book.CreatedAt)
            .ToList();
    }

    /// <summary>The reader's canonical position per book — latest across their devices
    /// by server receive time, the same rule as <see cref="ReadingProgressService.GetLatestAsync"/>.</summary>
    private async Task<Dictionary<Guid, ProgressMarker>> GetLatestProgressByBookAsync(
        Guid userId, IReadOnlyCollection<Guid> bookIds, CancellationToken ct)
    {
        var rows = await db.ReadingProgress
            .AsNoTracking()
            .Where(p => p.UserId == userId && bookIds.Contains(p.BookId))
            .GroupBy(p => p.BookId)
            .Select(g => g
                .OrderByDescending(p => p.UpdatedAt)
                .Select(p => new { p.BookId, p.SectionIndex, p.UpdatedAt })
                .First())
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.BookId,
            r => new ProgressMarker { SectionIndex = r.SectionIndex, UpdatedAt = r.UpdatedAt });
    }

    private sealed record ProgressMarker
    {
        public required int SectionIndex { get; init; }

        public required DateTimeOffset UpdatedAt { get; init; }
    }

    private static Task<Dictionary<Guid, int>> CountByBookAsync<T>(
        IQueryable<T> rows, Expression<Func<T, Guid>> bookId, CancellationToken ct) =>
        rows
            .GroupBy(bookId)
            .Select(g => new { BookId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.BookId, g => g.Count, ct);

    public async Task<Book?> FindAsync(Guid id, Guid userId, CancellationToken ct) =>
        await db.Books.AsNoTracking().VisibleTo(userId).FirstOrDefaultAsync(b => b.Id == id, ct);

    /// <summary>Flips a book between private and public (ADR-0023). Owner-only;
    /// a book the caller can't see reports <see cref="OwnedBookOutcome.NotFound"/>.</summary>
    public async Task<SetVisibilityResult> SetVisibilityAsync(
        Guid bookId, Guid userId, bool isPublic, CancellationToken ct)
    {
        var (outcome, book) = await db.Books.ResolveOwnedAsync(bookId, userId, ct);
        if (book is null)
        {
            return new SetVisibilityResult { Outcome = outcome };
        }

        if (book.IsPublic != isPublic)
        {
            book.IsPublic = isPublic;
            await db.SaveChangesAsync(ct);
        }

        return new SetVisibilityResult { Outcome = OwnedBookOutcome.Succeeded, Book = book };
    }
}

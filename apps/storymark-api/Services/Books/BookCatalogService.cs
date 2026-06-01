using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Services.Books;

/// <summary>Read access to the book catalogue for the library list and cover
/// endpoint. Aggregation/queries live here, not in the endpoints (layering).</summary>
internal sealed class BookCatalogService(StorymarkDbContext db, ISettingsService settings)
{
    public async Task<IReadOnlyList<BookCatalogEntry>> GetAllAsync(CancellationToken ct)
    {
        var books = await db.Books
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

        // Embedding progress is chunks already vectorised under the *current* model
        // over the book's total chunks — the same "current model only" filter search
        // uses (docs/plan.md §15.5), so a mid-reindex book counts honestly. Two plain
        // grouped counts rather than one filtered aggregate, to keep the SQL trivially
        // translatable.
        var modelId = settings.GetString("llm.embedding.modelId");
        var totalByBook = await CountByBookAsync(db.BookChunks, ct);
        var embeddedByBook = await CountByBookAsync(
            db.BookChunks.Where(c => c.Embedding != null && c.EmbeddingModelId == modelId), ct);

        return books
            .Select(b => new BookCatalogEntry
            {
                Book = b,
                TotalChunks = totalByBook.GetValueOrDefault(b.Id),
                EmbeddedChunks = embeddedByBook.GetValueOrDefault(b.Id),
            })
            .ToList();
    }

    private static Task<Dictionary<Guid, int>> CountByBookAsync(
        IQueryable<BookChunk> chunks, CancellationToken ct) =>
        chunks
            .GroupBy(c => c.BookId)
            .Select(g => new { BookId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.BookId, g => g.Count, ct);

    public async Task<Book?> FindAsync(Guid id, CancellationToken ct) =>
        await db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
}

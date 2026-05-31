using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Services.Books;

/// <summary>Read access to the book catalogue for the library list and cover
/// endpoint. Aggregation/queries live here, not in the endpoints (layering).</summary>
internal sealed class BookCatalogService(StorymarkDbContext db)
{
    public async Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken ct) =>
        await db.Books
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

    public async Task<Book?> FindAsync(Guid id, CancellationToken ct) =>
        await db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
}

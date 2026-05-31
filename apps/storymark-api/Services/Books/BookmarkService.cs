using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Services.Books;

/// <summary>Bookmark storage for the reader (docs/plan.md §12.6). Writes are
/// idempotent upserts keyed by the client-supplied id, so the offline queue can
/// replay them safely.</summary>
internal sealed class BookmarkService(StorymarkDbContext db)
{
    /// <summary>All bookmarks for a book, ordered by reading position, or
    /// <c>null</c> if the book does not exist.</summary>
    public async Task<IReadOnlyList<BookmarkView>?> ListAsync(Guid bookId, CancellationToken ct)
    {
        if (!await db.Books.AnyAsync(b => b.Id == bookId, ct))
        {
            return null;
        }

        return await db.BookBookmarks.AsNoTracking()
            .Where(b => b.BookId == bookId)
            .OrderBy(b => b.SectionIndex)
            .ThenBy(b => b.LocationStart)
            .Select(b => new BookmarkView
            {
                Id = b.Id,
                SectionIndex = b.SectionIndex,
                ParagraphIndex = b.ParagraphIndex,
                LocationStart = b.LocationStart,
                Label = b.Label,
                CreatedAt = b.CreatedAt,
            })
            .ToListAsync(ct);
    }

    /// <summary>Creates or replaces a bookmark by its id. Returns the saved view,
    /// or <c>null</c> if the book does not exist.</summary>
    public async Task<BookmarkView?> UpsertAsync(Guid bookId, SaveBookmarkInput input, CancellationToken ct)
    {
        if (!await db.Books.AnyAsync(b => b.Id == bookId, ct))
        {
            return null;
        }

        var existing = await db.BookBookmarks.FirstOrDefaultAsync(b => b.Id == input.Id, ct);
        if (existing is null)
        {
            db.BookBookmarks.Add(new BookBookmark
            {
                Id = input.Id,
                BookId = bookId,
                SectionIndex = input.SectionIndex,
                ParagraphIndex = input.ParagraphIndex,
                LocationStart = input.LocationStart,
                Label = input.Label,
                CreatedAt = input.CreatedAt,
            });
        }
        else
        {
            existing.BookId = bookId;
            existing.SectionIndex = input.SectionIndex;
            existing.ParagraphIndex = input.ParagraphIndex;
            existing.LocationStart = input.LocationStart;
            existing.Label = input.Label;
            existing.CreatedAt = input.CreatedAt;
        }

        await db.SaveChangesAsync(ct);

        return new BookmarkView
        {
            Id = input.Id,
            SectionIndex = input.SectionIndex,
            ParagraphIndex = input.ParagraphIndex,
            LocationStart = input.LocationStart,
            Label = input.Label,
            CreatedAt = input.CreatedAt,
        };
    }

    /// <summary>Removes a bookmark by id. Idempotent — a no-op if it is already
    /// gone.</summary>
    public async Task DeleteAsync(Guid bookId, Guid bookmarkId, CancellationToken ct)
    {
        await db.BookBookmarks
            .Where(b => b.Id == bookmarkId && b.BookId == bookId)
            .ExecuteDeleteAsync(ct);
    }
}

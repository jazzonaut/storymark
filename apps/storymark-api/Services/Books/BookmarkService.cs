using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Services.Books;

/// <summary>Bookmark storage for the reader (docs/plan.md §12.6). Writes are
/// idempotent upserts keyed by the client-supplied id, so the offline queue can
/// replay them safely.</summary>
internal sealed class BookmarkService(StorymarkDbContext db)
{
    /// <summary>The reader's bookmarks for a book, ordered by reading position, or
    /// <c>null</c> if the book does not exist.</summary>
    public async Task<IReadOnlyList<BookmarkView>?> ListAsync(Guid bookId, Guid userId, CancellationToken ct)
    {
        if (!await db.Books.IsVisibleAsync(bookId, userId, ct))
        {
            return null;
        }

        return await db.BookBookmarks.AsNoTracking()
            .Where(b => b.BookId == bookId && b.UserId == userId)
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

    /// <summary>Creates or replaces a bookmark by its id, scoped to the route book and
    /// the reader. Returns <see cref="BookmarkUpsertOutcome.BookNotFound"/> if the book
    /// does not exist, or <see cref="BookmarkUpsertOutcome.IdBelongsToAnotherOwner"/> if
    /// the id is already owned by a different book or a different reader (the mutation
    /// never reassigns a bookmark across either boundary).</summary>
    public async Task<BookmarkUpsertResult> UpsertAsync(
        Guid bookId,
        Guid userId,
        SaveBookmarkInput input,
        CancellationToken ct)
    {
        if (!await db.Books.IsVisibleAsync(bookId, userId, ct))
        {
            return new BookmarkUpsertResult { Outcome = BookmarkUpsertOutcome.BookNotFound };
        }

        var existing = await db.BookBookmarks.FirstOrDefaultAsync(b => b.Id == input.Id, ct);
        if (existing is not null && (existing.BookId != bookId || existing.UserId != userId))
        {
            return new BookmarkUpsertResult { Outcome = BookmarkUpsertOutcome.IdBelongsToAnotherOwner };
        }

        if (existing is null)
        {
            db.BookBookmarks.Add(new BookBookmark
            {
                Id = input.Id,
                BookId = bookId,
                UserId = userId,
                SectionIndex = input.SectionIndex,
                ParagraphIndex = input.ParagraphIndex,
                LocationStart = input.LocationStart,
                Label = input.Label,
                CreatedAt = input.CreatedAt,
            });
        }
        else
        {
            // existing matches (bookId, userId) here, so the upsert stays in scope.
            existing.SectionIndex = input.SectionIndex;
            existing.ParagraphIndex = input.ParagraphIndex;
            existing.LocationStart = input.LocationStart;
            existing.Label = input.Label;
            existing.CreatedAt = input.CreatedAt;
        }

        await db.SaveChangesAsync(ct);

        return new BookmarkUpsertResult
        {
            Outcome = BookmarkUpsertOutcome.Saved,
            View = new BookmarkView
            {
                Id = input.Id,
                SectionIndex = input.SectionIndex,
                ParagraphIndex = input.ParagraphIndex,
                LocationStart = input.LocationStart,
                Label = input.Label,
                CreatedAt = input.CreatedAt,
            },
        };
    }

    /// <summary>Removes the reader's bookmark by id. Idempotent — a no-op if it is
    /// already gone (or never theirs).</summary>
    public async Task DeleteAsync(Guid bookId, Guid userId, Guid bookmarkId, CancellationToken ct)
    {
        await db.BookBookmarks
            .Where(b => b.Id == bookmarkId && b.BookId == bookId && b.UserId == userId)
            .ExecuteDeleteAsync(ct);
    }
}

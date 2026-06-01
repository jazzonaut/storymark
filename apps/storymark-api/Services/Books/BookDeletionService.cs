using Microsoft.EntityFrameworkCore;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Storage;

namespace Storymark.Api.Services.Books;

/// <summary>Permanently removes a book and every artifact tied to it (docs/plan.md
/// §14). The <c>books</c> row is deleted first: every child table cascades at the
/// database level (the <c>OnDelete(Cascade)</c> foreign keys), so one statement
/// clears sections, paragraphs, chunks, entities, mentions, relationships, events,
/// summaries, memory snapshots, reading progress, bookmarks, and Q&amp;A. Only then
/// are the on-disk files removed — DB-first so a file-IO failure orphans recoverable
/// files rather than leaving a library row pointing at deleted content. <c>llm_calls</c>
/// is intentionally left intact: its <c>book_id</c> is nullable and unconstrained
/// because it is the spoiler-safety audit trail and cost meter (docs/plan.md §20.2),
/// which outlives the book.</summary>
internal sealed class BookDeletionService(StorymarkDbContext db, FileStorageService storage)
{
    /// <returns><c>true</c> if the book existed and was deleted; <c>false</c> if no
    /// book has that id.</returns>
    public async Task<bool> DeleteAsync(Guid bookId, CancellationToken ct)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == bookId, ct);
        if (book is null)
        {
            return false;
        }

        var contentHash = book.ContentHash;

        db.Books.Remove(book);
        await db.SaveChangesAsync(ct);

        storage.DeleteBookDirectory(contentHash);
        return true;
    }
}

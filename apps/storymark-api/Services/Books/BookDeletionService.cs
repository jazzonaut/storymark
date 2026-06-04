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
    /// <summary>Deletes the caller's book. Only the owner may delete (ADR-0023);
    /// a book the caller can't see reports <see cref="OwnedBookOutcome.NotFound"/>.</summary>
    public async Task<OwnedBookOutcome> DeleteAsync(Guid bookId, Guid userId, CancellationToken ct)
    {
        var (outcome, book) = await db.Books.ResolveOwnedAsync(bookId, userId, ct);
        if (book is null)
        {
            return outcome;
        }

        var contentHash = book.ContentHash;

        db.Books.Remove(book);
        await db.SaveChangesAsync(ct);

        // Per-owner copies of identical content share the on-disk directory
        // (dedup by hash, ADR-0023): remove it only once no row references it.
        if (!await db.Books.AnyAsync(b => b.ContentHash == contentHash, ct))
        {
            storage.DeleteBookDirectory(contentHash);
        }

        return OwnedBookOutcome.Succeeded;
    }
}

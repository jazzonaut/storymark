using Hangfire;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Jobs;

namespace Storymark.Api.Services.Books;

internal sealed record ReprocessBookResult
{
    public required bool Found { get; init; }

    public required bool Queued { get; init; }

    /// <summary>The status the book had when the request was handled — the terminal
    /// status that was reprocessed, or the mid-pipeline status that caused a
    /// rejection. Never the transient post-reset value.</summary>
    public required ProcessingStatus? Status { get; init; }
}

internal sealed record RecoverStuckBooksResult
{
    public required int BooksQueued { get; init; }

    public required IReadOnlyList<Guid> BookIds { get; init; }
}

/// <summary>Admin-only maintenance actions for the processing pipeline: recover
/// uploaded books that missed their initial Hangfire enqueue, and explicitly
/// reprocess terminal books after prompt/model/parser changes.</summary>
internal sealed class BookProcessingAdminService(
    StorymarkDbContext db,
    IBackgroundJobClient jobs)
{
    private static readonly HashSet<ProcessingStatus> ReprocessableStatuses =
    [
        ProcessingStatus.Indexed,
        ProcessingStatus.Failed,
    ];

    public async Task<RecoverStuckBooksResult> RecoverStuckUploadsAsync(
        TimeSpan staleAfter,
        CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(staleAfter);
        var books = await db.Books
            .Where(b => b.ProcessingStatus == ProcessingStatus.Uploaded && b.UpdatedAt <= cutoff)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(ct);

        foreach (var book in books)
        {
            book.ProcessingError = null;
            book.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        // Commit before enqueue (the BookImportService/ReprocessAsync ordering) so a
        // failed save cannot leave orphaned queued jobs.
        foreach (var book in books)
        {
            jobs.Enqueue<ProcessBookJob>(j => j.RunAsync(book.Id, CancellationToken.None));
        }
        return new RecoverStuckBooksResult
        {
            BooksQueued = books.Count,
            BookIds = books.Select(b => b.Id).ToList(),
        };
    }

    public async Task<ReprocessBookResult> ReprocessAsync(Guid bookId, CancellationToken ct)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == bookId, ct);
        if (book is null)
        {
            return new ReprocessBookResult { Found = false, Queued = false, Status = null };
        }

        if (!ReprocessableStatuses.Contains(book.ProcessingStatus))
        {
            return new ReprocessBookResult
            {
                Found = true,
                Queued = false,
                Status = book.ProcessingStatus,
            };
        }

        var previousStatus = book.ProcessingStatus;

        // One transaction around the clears and the status reset: ExecuteDelete
        // auto-commits per statement outside of one, and a crash after the deletes
        // but before the reset would leave a book that reads Indexed with no
        // derived data behind it.
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await ClearDerivedDataAsync(bookId, ct);
            book.ProcessingStatus = ProcessingStatus.Uploaded;
            book.ProcessingError = null;
            book.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        jobs.Enqueue<ProcessBookJob>(j => j.RunAsync(book.Id, CancellationToken.None));
        return new ReprocessBookResult
        {
            Found = true,
            Queued = true,
            Status = previousStatus,
        };
    }

    /// <summary>Only these six tables need explicit deletes: paragraphs and chunks
    /// cascade from sections, and mentions, relationships, and entity summaries cascade
    /// from entities — DB-level FKs, so they fire under <c>ExecuteDelete</c> too.
    /// Reading progress, bookmarks, and <c>llm_calls</c> are intentionally kept:
    /// progress and bookmarks survive a reprocess, and the call log is the audit
    /// trail that outlives derived data (same reasoning as BookDeletionService).</summary>
    private async Task ClearDerivedDataAsync(Guid bookId, CancellationToken ct)
    {
        await db.BookSections.Where(s => s.BookId == bookId).ExecuteDeleteAsync(ct);
        await db.BookEntities.Where(e => e.BookId == bookId).ExecuteDeleteAsync(ct);
        await db.BookEvents.Where(e => e.BookId == bookId).ExecuteDeleteAsync(ct);
        await db.SectionSummaries.Where(s => s.BookId == bookId).ExecuteDeleteAsync(ct);
        await db.BookMemorySnapshots.Where(s => s.BookId == bookId).ExecuteDeleteAsync(ct);
        await db.QuestionAnswers.Where(q => q.BookId == bookId).ExecuteDeleteAsync(ct);
    }
}

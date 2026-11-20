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

    /// <summary>The status observed when the request was handled.</summary>
    public required ProcessingStatus? Status { get; init; }
}

internal sealed record RecoverStuckBooksResult
{
    public required int BooksQueued { get; init; }

    public required IReadOnlyList<Guid> BookIds { get; init; }
}

/// <summary>Admin-only maintenance actions for the processing pipeline: recover
/// uploaded books that missed their initial Hangfire enqueue, and explicitly
/// reprocess books after prompt/model/parser changes.</summary>
internal sealed class BookProcessingAdminService(
    StorymarkDbContext db,
    IBackgroundJobClient jobs)
{
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

        // Commit before enqueue (the BookImportService ordering) so a
        // failed save cannot leave orphaned queued jobs.
        foreach (var book in books)
        {
            jobs.Enqueue<ProcessBookJob>(j => j.RunAsync(book.Id, book.ProcessingRunId, CancellationToken.None));
        }
        return new RecoverStuckBooksResult
        {
            BooksQueued = books.Count,
            BookIds = books.Select(b => b.Id).ToList(),
        };
    }

    public async Task<ReprocessBookResult> ReprocessAsync(Guid bookId, Guid? expectedRunId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var book = await db.Books.FromSql($"SELECT * FROM books WHERE id = {bookId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (book is null)
        {
            return new ReprocessBookResult { Found = false, Queued = false, Status = null };
        }

        if (book.ProcessingStatus == ProcessingStatus.Restarting
            || (expectedRunId.HasValue && expectedRunId != book.ProcessingRunId)
            || (!expectedRunId.HasValue && book.ProcessingStatus is not (ProcessingStatus.Indexed or ProcessingStatus.Failed)))
        {
            return new ReprocessBookResult
            {
                Found = true,
                Queued = false,
                Status = book.ProcessingStatus,
            };
        }

        var previousStatus = book.ProcessingStatus;

        var runId = Guid.NewGuid();
        book.ProcessingRunId = runId;
        book.ProcessingStatus = ProcessingStatus.Restarting;
        book.ProcessingError = null;
        book.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        // Queue before commit. The worker locks the book row before checking runId.
        // An enqueue failure rolls back and a lost commit makes that job a no-op.
        jobs.Enqueue<ProcessBookJob>(j => j.RunAsync(bookId, runId, CancellationToken.None));
        await transaction.CommitAsync(ct);
        return new ReprocessBookResult
        {
            Found = true,
            Queued = true,
            Status = previousStatus,
        };
    }
}

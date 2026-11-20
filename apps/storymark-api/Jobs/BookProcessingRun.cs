using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Jobs;

/// <summary>Serializes pipeline stages per book across workers. The run ID also
/// fences queued jobs and status writes after an administrator requests a restart.</summary>
internal sealed class BookProcessingRun(StorymarkDbContext db, IServiceScopeFactory scopes)
{
    public async Task AdvanceAsync(Book book, ProcessingStatus status, Action enqueue, CancellationToken ct)
    {
        // The successor checks the committed status after it acquires the same
        // session lock. A crash cannot leave a committed handoff without a job.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        book.ProcessingStatus = status;
        book.ProcessingError = null;
        book.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        enqueue();
        await transaction.CommitAsync(ct);
    }

    public async Task ExecuteAsync(Guid bookId, Guid runId, Func<Book, CancellationToken, Task> stage, CancellationToken ct)
    {
        // Hold the session lock across stage commits, model calls and handoff. The
        // replacement may clear derived rows only after the old writer has exited.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var acquired = db.Database.SqlQuery<bool>(
                $"SELECT pg_try_advisory_lock(hashtextextended({bookId}::text, 0)) AS \"Value\"");
            while (!await acquired.SingleAsync(ct))
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }

            try
            {
                // A restart enqueues before committing its new ID. Lock by book ID
                // alone so an early pickup waits for that transaction's outcome.
                var book = await db.Books.FromSql($"SELECT * FROM books WHERE id = {bookId} FOR UPDATE")
                    .SingleOrDefaultAsync(ct);
                if (book is null || book.ProcessingRunId != runId)
                {
                    return;
                }

                using var running = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var monitor = CancelWhenReplacedAsync(bookId, runId, running);
                try
                {
                    await stage(book, running.Token);
                }
                catch (OperationCanceledException) when (running.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    // Superseded. Let the replacement acquire the lock.
                }
                catch (DbUpdateConcurrencyException) when (!ct.IsCancellationRequested)
                {
                    if (await db.Books.AnyAsync(b => b.Id == bookId && b.ProcessingRunId == runId, ct))
                    {
                        throw;
                    }
                }
                finally
                {
                    await running.CancelAsync();
                    await monitor;
                }
            }
            finally
            {
                await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock(hashtextextended({bookId}::text, 0))", CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task CancelWhenReplacedAsync(Guid bookId, Guid runId, CancellationTokenSource running)
    {
        await using var scope = scopes.CreateAsyncScope();
        var observer = scope.ServiceProvider.GetRequiredService<StorymarkDbContext>();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(running.Token))
            {
                if (!await observer.Books.AnyAsync(b => b.Id == bookId && b.ProcessingRunId == runId, running.Token))
                {
                    await running.CancelAsync();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (running.IsCancellationRequested)
        {
            // Stage completed, was replaced, or the worker is shutting down.
        }
        catch
        {
            await running.CancelAsync();
            throw;
        }
    }
}

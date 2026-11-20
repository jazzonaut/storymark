using Hangfire;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Storymark.Api.Domain.Books;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Llm;
using Storymark.Api.Services.Llm;

namespace Storymark.Api.Jobs;

/// <summary>Embeds a book's chunks (docs/plan.md §15.2 step 16) and finishes the
/// processing pipeline: it fills <see cref="BookChunk.Embedding"/> and the model
/// tags via <see cref="ChatModelRouter.EmbedAsync"/> (so every call lands in
/// <c>llm_calls</c>), then drives <see cref="ProcessingStatus"/> from
/// <c>Embedding</c> to <c>Indexed</c>.
///
/// <para>This is also the per-book reindex worker (S3.4): because it embeds chunks
/// "missing a vector OR tagged with a different model id", re-running it after the
/// <c>llm.embedding.modelId</c> setting changes re-embeds every chunk under the new
/// model and overwrites in place — so the swap endpoint just re-enqueues it per book
/// rather than duplicating the loop in a separate ReindexBookJob.</para>
///
/// <para>Idempotent and resumable: it only embeds chunks that are missing a vector
/// or were embedded under a different model id, so a retry after a partial failure
/// (or a reindex) picks up where it left off. Transient failures (a timeout, a
/// provider 5xx) record their reason on <see cref="Book.ProcessingError"/> and
/// rethrow so Hangfire retries, leaving the book on <c>Embedding</c>. A missing
/// model (<see cref="ModelNotFoundException"/>) is deterministic, so it fails the
/// book to <c>Failed</c> with that reason and does not retry.</para></summary>
internal sealed class EmbeddingJob(
    StorymarkDbContext db,
    BookProcessingRun run,
    ChatModelRouter router,
    LlmRegistryService registry,
    ILogger<EmbeddingJob> logger)
{
    private const int BatchSize = 32;

    public Task RunAsync(Guid bookId, CancellationToken ct) => RunAsync(bookId, Guid.Empty, ct);

    public Task RunAsync(Guid bookId, Guid runId, CancellationToken ct) =>
        run.ExecuteAsync(bookId, runId, ProcessAsync, ct);

    private async Task ProcessAsync(Book book, CancellationToken ct)
    {
        var bookId = book.Id;
        if (book.ProcessingStatus is not (ProcessingStatus.Embedding or ProcessingStatus.Indexed))
        {
            return;
        }

        var embeddingModel = await registry.ResolveEmbeddingAsync(ct);
        var modelId = embeddingModel.EmbeddingIdentity;
        var modelVersion = embeddingModel.ModelName;

        // Chunks needing the current model: never embedded, or embedded under a
        // different model. The latter case is exactly a reindex (S3.4) — the model
        // setting changed, so every chunk re-embeds and overwrites in place.
        var pending = await db.BookChunks
            .Where(c => c.BookId == bookId && (c.Embedding == null || c.EmbeddingModelId != modelId))
            .OrderBy(c => c.SectionIndex)
            .ThenBy(c => c.ChunkIndex)
            .ToListAsync(ct);

        logger.LogInformation(
            "EmbeddingJob for book {BookId}: {PendingCount} chunks to embed with {ModelId}",
            bookId, pending.Count, modelId);

        if (pending.Count > 0 && book.ProcessingStatus != ProcessingStatus.Embedding)
        {
            book.ProcessingStatus = ProcessingStatus.Embedding;
            await db.SaveChangesAsync(ct);
        }

        try
        {
            await EmbedAllAsync(book, pending, modelId, modelVersion, ct);
        }
        catch (ModelNotFoundException ex)
        {
            // Deterministic config error: the model isn't pulled, so all 10 Hangfire
            // retries would fail identically. Fail the book fast with the reason and
            // don't rethrow, so the job isn't rescheduled. Requeue after pulling the model.
            logger.LogError(ex, "EmbeddingJob failed for book {BookId}: model unavailable", bookId);
            book.ProcessingStatus = ProcessingStatus.Failed;
            book.ProcessingError = ex.Message;
            book.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Likely transient (timeout, provider 5xx): record why it stalled so the
            // library shows it, then rethrow to let Hangfire retry. The book stays on
            // Embedding and a later attempt resumes from the unembedded chunks.
            logger.LogWarning(ex, "EmbeddingJob attempt failed for book {BookId}; will retry", bookId);
            book.ProcessingError = ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task EmbedAllAsync(
        Book book, List<BookChunk> pending, string modelId, string modelVersion, CancellationToken ct)
    {
        var embedded = 0;
        foreach (var chunk in pending)
        {
            var vector = await router.EmbedAsync(
                chunk.Text,
                new LlmCallMetadata { BookId = book.Id, SectionIndex = chunk.SectionIndex },
                ct);

            chunk.Embedding = new Vector(vector.ToArray());
            chunk.EmbeddingModelId = modelId;
            chunk.EmbeddingModelVersion = modelVersion;
            embedded++;

            if (embedded % BatchSize == 0)
            {
                await db.SaveChangesAsync(ct);
                logger.LogInformation(
                    "EmbeddingJob for book {BookId}: {Embedded}/{Total}", book.Id, embedded, pending.Count);
            }
        }

        await db.SaveChangesAsync(ct);

        book.ProcessingStatus = ProcessingStatus.Indexed;
        book.ProcessingError = null;
        book.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Indexed book {BookId}: embedded {Embedded} chunks", book.Id, embedded);
    }
}

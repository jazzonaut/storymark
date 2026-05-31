using Hangfire;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Storymark.Api.Domain.Books;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Services.Llm;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Jobs;

/// <summary>Embeds a book's chunks (docs/plan.md §15.2 step 16) and finishes the
/// processing pipeline: it fills <see cref="BookChunk.Embedding"/> and the model
/// tags via <see cref="ChatModelRouter.EmbedAsync"/> (so every call lands in
/// <c>llm_calls</c>), then drives <see cref="ProcessingStatus"/> from
/// <c>Embedding</c> to <c>Indexed</c>.
///
/// <para>Idempotent and resumable: it only embeds chunks that are missing a vector
/// or were embedded under a different model id, so a retry after a partial failure
/// (or a future re-embed) picks up where it left off. Because of that it allows
/// Hangfire retries — embedding failures (model loading, a timeout) are usually
/// transient, unlike a parse failure. A book whose retries are all exhausted stays
/// at <c>Embedding</c> with a failed job visible in the dashboard.</para></summary>
internal sealed class EmbeddingJob(
    StorymarkDbContext db,
    ChatModelRouter router,
    ISettingsService settings,
    ILogger<EmbeddingJob> logger)
{
    private const int BatchSize = 32;

    public async Task RunAsync(Guid bookId, CancellationToken ct)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == bookId, ct);
        if (book is null)
        {
            logger.LogWarning("EmbeddingJob skipped: book {BookId} not found", bookId);
            return;
        }

        var modelId = settings.GetString("llm.embedding.modelId");
        var modelVersion = modelId[(modelId.IndexOf(':', StringComparison.Ordinal) + 1)..];

        var pending = await db.BookChunks
            .Where(c => c.BookId == bookId && (c.Embedding == null || c.EmbeddingModelId != modelId))
            .OrderBy(c => c.SectionIndex)
            .ThenBy(c => c.ChunkIndex)
            .ToListAsync(ct);

        logger.LogInformation(
            "EmbeddingJob for book {BookId}: {PendingCount} chunks to embed with {ModelId}",
            bookId, pending.Count, modelId);

        var embedded = 0;
        foreach (var chunk in pending)
        {
            var vector = await router.EmbedAsync(
                chunk.Text,
                new LlmCallMetadata { BookId = bookId, SectionIndex = chunk.SectionIndex },
                ct);

            if (vector.Count != BookChunk.EmbeddingDimensions)
            {
                throw new InvalidOperationException(
                    $"Embedding model '{modelId}' returned {vector.Count} dimensions; " +
                    $"the book_chunks.embedding column is vector({BookChunk.EmbeddingDimensions}).");
            }

            chunk.Embedding = new Vector(vector.ToArray());
            chunk.EmbeddingModelId = modelId;
            chunk.EmbeddingModelVersion = modelVersion;
            embedded++;

            if (embedded % BatchSize == 0)
            {
                await db.SaveChangesAsync(ct);
                logger.LogInformation(
                    "EmbeddingJob for book {BookId}: {Embedded}/{Total}", bookId, embedded, pending.Count);
            }
        }

        await db.SaveChangesAsync(ct);

        book.ProcessingStatus = ProcessingStatus.Indexed;
        book.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Indexed book {BookId}: embedded {Embedded} chunks", bookId, embedded);
    }
}

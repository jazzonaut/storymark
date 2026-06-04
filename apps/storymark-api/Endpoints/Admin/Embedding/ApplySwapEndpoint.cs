using System.Text.Json;
using FastEndpoints;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Jobs;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Endpoints.Admin.Embedding;

internal sealed record ApplySwapRequest
{
    public required string ModelId { get; init; }
}

internal sealed record ApplySwapResponse
{
    public required string ModelId { get; init; }

    public required int BooksQueued { get; init; }
}

/// <summary>Applies an embedding-model swap (docs/plan.md §15.5): persists the new
/// <c>llm.embedding.modelId</c>, then enqueues a per-book reindex (<see cref="EmbeddingJob"/>,
/// which re-embeds every chunk under the new model). Search filters by the active model
/// id, so a book's results converge to complete as its reindex finishes. Admin-only.</summary>
internal sealed class ApplySwapEndpoint(
    ISettingsService settings,
    StorymarkDbContext db,
    IBackgroundJobClient jobs)
    : Endpoint<ApplySwapRequest, ApplySwapResponse>
{
    public override void Configure()
    {
        Post("embedding/apply-swap");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(ApplySwapRequest req, CancellationToken ct)
    {
        var modelId = req.ModelId.Trim();
        if (!Guid.TryParse(modelId, out var modelGuid) || !await db.LlmModels.AnyAsync(m => m.Id == modelGuid, ct))
        {
            ThrowError(r => r.ModelId, "modelId must be the id of a configured model.");
        }

        await settings.UpdateAsync(LlmSettingKeys.EmbeddingModelId, JsonSerializer.Serialize(modelId), User.UserName(), ct);

        // Reindex only books that have finished the import pipeline (Indexed) or are
        // mid-embedding (an interrupted reindex). EmbeddingJob unconditionally drives a
        // book to Indexed when its chunks are embedded, so enqueuing it for a Failed or
        // still-extracting book would falsely mark it Indexed and skip the rest of the
        // pipeline — those books reindex naturally once they next reach Indexed.
        var bookIds = await db.Books
            .Where(b => (b.ProcessingStatus == ProcessingStatus.Indexed
                    || b.ProcessingStatus == ProcessingStatus.Embedding)
                && db.BookChunks.Any(c => c.BookId == b.Id))
            .Select(b => b.Id)
            .ToListAsync(ct);
        foreach (var bookId in bookIds)
        {
            jobs.Enqueue<EmbeddingJob>(j => j.RunAsync(bookId, CancellationToken.None));
        }

        await Send.OkAsync(new ApplySwapResponse { ModelId = modelId, BooksQueued = bookIds.Count }, ct);
    }
}

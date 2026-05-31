using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Endpoints.Admin.Embedding;

internal sealed record EmbeddingImpactResponse
{
    public required int BooksAffected { get; init; }

    public required int ChunkCount { get; init; }

    public required int EstimatedSeconds { get; init; }
}

/// <summary>Impact preview for an embedding-model swap (docs/plan.md §15.5): how many
/// books and chunks a reindex would re-embed, plus a rough time estimate. Every chunk
/// is re-embedded on a model change, so this is simply the full corpus. Admin-only.</summary>
internal sealed class ImpactPreviewEndpoint(StorymarkDbContext db)
    : EndpointWithoutRequest<EmbeddingImpactResponse>
{
    private const double FallbackSecondsPerChunk = 0.5;
    private const int LatencySampleSize = 50;

    public override void Configure()
    {
        Get("embedding/impact-preview");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var chunkCount = await db.BookChunks.CountAsync(ct);
        var booksAffected = await db.BookChunks.Select(c => c.BookId).Distinct().CountAsync(ct);

        // Estimate from recent chunk-embed latencies. SectionIndex is set for chunk
        // embeds but null for query embeds, so this samples only the comparable calls.
        var samples = await db.LlmCalls
            .Where(c => c.Task == "embed" && c.SectionIndex != null && c.LatencyMs != null)
            .OrderByDescending(c => c.CreatedAt)
            .Take(LatencySampleSize)
            .Select(c => c.LatencyMs)
            .ToListAsync(ct);

        // Already filtered to non-null LatencyMs in SQL, so GetValueOrDefault is exact.
        var latencies = samples.Select(l => l.GetValueOrDefault()).ToList();
        var secondsPerChunk = latencies.Count > 0 ? latencies.Average() / 1000.0 : FallbackSecondsPerChunk;

        await Send.OkAsync(
            new EmbeddingImpactResponse
            {
                BooksAffected = booksAffected,
                ChunkCount = chunkCount,
                EstimatedSeconds = (int)Math.Ceiling(chunkCount * secondsPerChunk),
            },
            ct);
    }
}

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Services.Llm;

namespace Storymark.Api.Endpoints.Debug;

internal sealed record SearchHit
{
    public required int SectionIndex { get; init; }

    public required int ChunkIndex { get; init; }

    public required int LocationStart { get; init; }

    public required int LocationEnd { get; init; }

    public required double Score { get; init; }

    public required string Text { get; init; }
}

internal sealed record PostSearchResponse
{
    public required Guid BookId { get; init; }

    public required string EmbeddingModelId { get; init; }

    public required IReadOnlyList<SearchHit> Hits { get; init; }
}

/// <summary>Top-K nearest chunks for a query string by cosine similarity (docs/plan.md
/// §13), using the HNSW index via the <c>&lt;=&gt;</c> operator. No spoiler filter yet —
/// that is S3.3's <c>SpoilerSafeRetrievalService</c>. Gated by
/// <c>Storymark:DebugEndpointsEnabled</c>; returns 404 when disabled.</summary>
internal sealed class PostSearchEndpoint(
    IConfiguration configuration,
    StorymarkDbContext db,
    ChatModelRouter router,
    LlmRegistryService registry)
    : EndpointWithoutRequest<PostSearchResponse>
{
    private const int DefaultK = 5;
    private const int MaxK = 50;

    public override void Configure()
    {
        Post("_debug/search");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Storymark:DebugEndpointsEnabled"))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var bookId = Query<Guid>("bookId", isRequired: true);
        var q = Query<string>("q", isRequired: true);
        if (string.IsNullOrWhiteSpace(q))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var requestedK = Query<int>("k", isRequired: false);
        var k = Math.Clamp(requestedK > 0 ? requestedK : DefaultK, 1, MaxK);
        var modelId = await registry.ResolveEmbeddingIdentityAsync(ct);

        var embedding = await router.EmbedAsync(q, new LlmCallMetadata { BookId = bookId }, ct);
        var queryVector = new Vector(embedding.ToArray());

        var chunks = await db.BookChunks
            .FromSql(
                $@"SELECT * FROM book_chunks
                   WHERE book_id = {bookId} AND embedding_model_id = {modelId} AND embedding IS NOT NULL
                   ORDER BY embedding <=> {queryVector}
                   LIMIT {k}")
            .AsNoTracking()
            .ToListAsync(ct);

        var queryArray = queryVector.ToArray();
        var hits = new List<SearchHit>(chunks.Count);
        foreach (var chunk in chunks)
        {
            if (chunk.Embedding is not { } chunkVector)
            {
                continue;
            }

            hits.Add(new SearchHit
            {
                SectionIndex = chunk.SectionIndex,
                ChunkIndex = chunk.ChunkIndex,
                LocationStart = chunk.LocationStart,
                LocationEnd = chunk.LocationEnd,
                Score = CosineSimilarity(queryArray, chunkVector.ToArray()),
                Text = chunk.Text,
            });
        }

        await Send.OkAsync(
            new PostSearchResponse { BookId = bookId, EmbeddingModelId = modelId, Hits = hits },
            ct);
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom == 0 ? 0 : dot / denom;
    }
}

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Endpoints.Debug;

internal sealed record ChunkView
{
    public required int SectionIndex { get; init; }

    public required int ChunkIndex { get; init; }

    public required int LocationStart { get; init; }

    public required int LocationEnd { get; init; }

    public required int TokenEstimate { get; init; }

    public required int CharCount { get; init; }

    public required string Text { get; init; }

    public required bool HasEmbedding { get; init; }
}

internal sealed record GetChunksResponse
{
    public required Guid BookId { get; init; }

    public required int ChunkCount { get; init; }

    public required IReadOnlyList<ChunkView> Chunks { get; init; }
}

/// <summary>Lists a book's chunks (ordered by section then chunk index) for verifying
/// S3.1 chunking — sizes, offsets, and overlap. Gated by
/// <c>Storymark:DebugEndpointsEnabled</c>; returns 404 when disabled.</summary>
internal sealed class GetChunksEndpoint(IConfiguration configuration, StorymarkDbContext db)
    : EndpointWithoutRequest<GetChunksResponse>
{
    public override void Configure()
    {
        Get("_debug/chunks/{bookId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Storymark:DebugEndpointsEnabled"))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var bookId = Route<Guid>("bookId");

        var chunks = await db.BookChunks
            .Where(c => c.BookId == bookId)
            .OrderBy(c => c.SectionIndex)
            .ThenBy(c => c.ChunkIndex)
            .Select(c => new ChunkView
            {
                SectionIndex = c.SectionIndex,
                ChunkIndex = c.ChunkIndex,
                LocationStart = c.LocationStart,
                LocationEnd = c.LocationEnd,
                TokenEstimate = c.TokenEstimate,
                CharCount = c.Text.Length,
                Text = c.Text,
                HasEmbedding = c.Embedding != null,
            })
            .ToListAsync(ct);

        await Send.OkAsync(
            new GetChunksResponse
            {
                BookId = bookId,
                ChunkCount = chunks.Count,
                Chunks = chunks,
            },
            ct);
    }
}

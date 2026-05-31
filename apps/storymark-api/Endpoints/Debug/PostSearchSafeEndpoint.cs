using FastEndpoints;
using Storymark.Api.Services.Retrieval;

namespace Storymark.Api.Endpoints.Debug;

internal sealed record SafeSearchHit
{
    public required int SectionIndex { get; init; }

    public required int ChunkIndex { get; init; }

    public required int LocationStart { get; init; }

    public required int LocationEnd { get; init; }

    public required double Score { get; init; }

    public required string Text { get; init; }
}

internal sealed record PostSearchSafeResponse
{
    public required Guid BookId { get; init; }

    public required int CurrentSection { get; init; }

    public required int CurrentLocation { get; init; }

    public required IReadOnlyList<SafeSearchHit> Hits { get; init; }
}

/// <summary>Inspects spoiler-safe hybrid retrieval (docs/plan.md §13): top-K reranked
/// chunks at a reading position, with the spoiler filter applied. Use it to confirm no
/// future content leaks. Gated by <c>Storymark:DebugEndpointsEnabled</c>; 404 when off.</summary>
internal sealed class PostSearchSafeEndpoint(
    IConfiguration configuration,
    SpoilerSafeRetrievalService retrieval)
    : EndpointWithoutRequest<PostSearchSafeResponse>
{
    private const int DefaultK = 8;
    private const int MaxK = 50;

    public override void Configure()
    {
        Post("_debug/search-safe");
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

        var currentSection = Query<int>("section", isRequired: true);
        var currentLocation = Query<int>("location", isRequired: true);
        var requestedK = Query<int>("k", isRequired: false);
        var k = Math.Clamp(requestedK > 0 ? requestedK : DefaultK, 1, MaxK);

        var evidence = await retrieval.SearchAsync(bookId, q, currentSection, currentLocation, k, ct);

        await Send.OkAsync(
            new PostSearchSafeResponse
            {
                BookId = bookId,
                CurrentSection = currentSection,
                CurrentLocation = currentLocation,
                Hits = evidence
                    .Select(e => new SafeSearchHit
                    {
                        SectionIndex = e.SectionIndex,
                        ChunkIndex = e.ChunkIndex,
                        LocationStart = e.LocationStart,
                        LocationEnd = e.LocationEnd,
                        Score = e.Score,
                        Text = e.Text,
                    })
                    .ToList(),
            },
            ct);
    }
}

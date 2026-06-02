using FastEndpoints;
using Storymark.Api.Services.Books;
using Storymark.Api.Services.Entities;

namespace Storymark.Api.Endpoints.Books;

internal sealed record MergeEntitiesRequest
{
    /// <summary>The entities to fold into the survivor (the route's <c>survivorId</c>),
    /// whose canonical name is kept.</summary>
    public required IReadOnlyList<Guid> LoserIds { get; init; }
}

/// <summary>Merges one or more duplicate entities into a survivor when the extractor split
/// one real person/place/thing across rows (docs/plan.md §16). Public, like the other
/// reader endpoints. The survivor keeps its canonical name; the losers' names fold into its
/// aliases. 400 if no losers are given; 404 if the book, survivor, or any loser is unknown
/// or belongs to another book.</summary>
internal sealed class MergeEntitiesEndpoint(EntityMergeService merge)
    : Endpoint<MergeEntitiesRequest, StoryGuideEntityView>
{
    public override void Configure()
    {
        Post("books/{bookId}/entities/{survivorId}/merge");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MergeEntitiesRequest req, CancellationToken ct)
    {
        if (req.LoserIds.Count == 0)
        {
            await Send.StringAsync("Provide at least one entity to merge.", 400, cancellation: ct);
            return;
        }

        var survivor = await merge.MergeAsync(
            Route<Guid>("bookId"),
            Route<Guid>("survivorId"),
            req.LoserIds,
            ct);

        if (survivor is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(survivor, ct);
    }
}

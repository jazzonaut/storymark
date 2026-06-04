using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Books;
using Storymark.Api.Services.Entities;

namespace Storymark.Api.Endpoints.Books;

/// <summary>A spoiler-safe entity summary at the reader's position (docs/plan.md §16.3),
/// generated lazily on first request and cached. 404 if the book or entity is unknown, or the entity is not yet visible at
/// the position. <c>section</c>/<c>location</c> are required query params.</summary>
internal sealed class GetEntitySummaryEndpoint(EntitySummaryService summaries)
    : EndpointWithoutRequest<EntitySummaryView>
{
    public override void Configure()
    {
        Get("books/{id}/entities/{entityId}/summary");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var summary = await summaries.GetOrCreateAsync(
            Route<Guid>("id"),
            User.UserId(),
            Route<Guid>("entityId"),
            Query<int>("section", isRequired: true),
            Query<int>("location", isRequired: true),
            ct);

        if (summary is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(summary, ct);
    }
}

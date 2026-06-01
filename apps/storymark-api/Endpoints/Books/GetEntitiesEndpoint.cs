using FastEndpoints;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>The Story Guide list: entities the reader has met, with a cached short
/// summary where one exists (docs/plan.md §5.3, §16). Public, like the other reader
/// endpoints. 404 if the book is unknown. <c>upToSection</c>/<c>upToLocation</c> are
/// required query params — the spoiler boundary.</summary>
internal sealed class GetEntitiesEndpoint(BookEntityService entities)
    : EndpointWithoutRequest<IReadOnlyList<StoryGuideEntityView>>
{
    public override void Configure()
    {
        Get("books/{id}/entities");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var list = await entities.GetVisibleListAsync(
            Route<Guid>("id"),
            Query<int>("upToSection", isRequired: true),
            Query<int>("upToLocation", isRequired: true),
            ct);

        if (list is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(list, ct);
    }
}

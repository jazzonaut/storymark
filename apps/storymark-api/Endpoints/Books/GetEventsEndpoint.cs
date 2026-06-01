using FastEndpoints;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>Events visible at the reader's position (docs/plan.md §5.1, §19.2),
/// spoiler-filtered server-side. Public, like the other reader endpoints. 404 if
/// the book is unknown. <c>upToSection</c>/<c>upToLocation</c> are required query
/// params — the spoiler boundary, not an optional convenience.</summary>
internal sealed class GetEventsEndpoint(BookExtractionService extraction)
    : EndpointWithoutRequest<IReadOnlyList<EventView>>
{
    public override void Configure()
    {
        Get("books/{id}/events");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var events = await extraction.GetEventsAsync(
            Route<Guid>("id"),
            Query<int>("upToSection", isRequired: true),
            Query<int>("upToLocation", isRequired: true),
            ct);

        if (events is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(events, ct);
    }
}

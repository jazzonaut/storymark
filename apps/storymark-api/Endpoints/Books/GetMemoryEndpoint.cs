using FastEndpoints;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>The rolling "story so far" at-or-before the reader's position
/// (docs/plan.md §5.2) — the data source for the section-entry recap card. Public,
/// like the other reader endpoints. 404 if the book is unknown, 204 if it exists but
/// has no snapshot at-or-before the position yet. <c>upToSection</c>/<c>upToLocation</c>
/// are required query params — the spoiler boundary.</summary>
internal sealed class GetMemoryEndpoint(BookMemoryService memory)
    : EndpointWithoutRequest<MemoryView>
{
    public override void Configure()
    {
        Get("books/{id}/memory");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var bookId = Route<Guid>("id");
        var upToSection = Query<int>("upToSection", isRequired: true);
        var upToLocation = Query<int>("upToLocation", isRequired: true);

        var latest = await memory.GetLatestAsync(bookId, upToSection, upToLocation, ct);
        if (latest is not null)
        {
            await Send.OkAsync(latest, ct);
            return;
        }

        if (await memory.BookExistsAsync(bookId, ct))
        {
            await Send.NoContentAsync(ct);
            return;
        }

        await Send.NotFoundAsync(ct);
    }
}

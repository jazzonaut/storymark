using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>The rolling "story so far" at-or-before the reader's position
/// (docs/plan.md §5.2) — the data source for the section-entry recap card. 404 if the book is unknown, 204 if it exists but
/// has no snapshot at-or-before the position yet. <c>upToSection</c>/<c>upToLocation</c>
/// are required query params — the spoiler boundary.</summary>
internal sealed class GetMemoryEndpoint(BookMemoryService memory, StorymarkDbContext db)
    : EndpointWithoutRequest<MemoryView>
{
    public override void Configure()
    {
        Get("books/{id}/memory");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var bookId = Route<Guid>("id");
        var userId = User.UserId();
        var upToSection = Query<int>("upToSection", isRequired: true);
        var upToLocation = Query<int>("upToLocation", isRequired: true);

        // Visibility first, then the snapshot — the same shape as GetProgress, so
        // the gate runs exactly once per request (GetLatestAsync doesn't re-check).
        if (!await db.Books.IsVisibleAsync(bookId, userId, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var latest = await memory.GetLatestAsync(bookId, upToSection, upToLocation, ct);
        if (latest is not null)
        {
            await Send.OkAsync(latest, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

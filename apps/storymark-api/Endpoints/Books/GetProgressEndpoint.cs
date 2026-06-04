using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>The canonical reading position for a book — the latest across all
/// devices (docs/plan.md §12.5). 404 if the book is unknown, 204 if it
/// exists but has no recorded progress yet.</summary>
internal sealed class GetProgressEndpoint(ReadingProgressService progress, StorymarkDbContext db)
    : EndpointWithoutRequest<ProgressView>
{
    public override void Configure()
    {
        Get("books/{id}/progress");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var bookId = Route<Guid>("id");
        var userId = User.UserId();

        // Visibility first: a non-visible book must stay a 404 (ADR-0023).
        // Reader-state rows are never deleted by a visibility flip — for a
        // non-owner they are unreachable while the book is private and
        // resurface if the owner re-publishes.
        if (!await db.Books.IsVisibleAsync(bookId, userId, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var latest = await progress.GetLatestAsync(bookId, userId, ct);
        if (latest is not null)
        {
            await Send.OkAsync(latest, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

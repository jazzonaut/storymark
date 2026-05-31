using FastEndpoints;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>The canonical reading position for a book — the latest across all
/// devices (docs/plan.md §12.5). Public. 404 if the book is unknown, 204 if it
/// exists but has no recorded progress yet.</summary>
internal sealed class GetProgressEndpoint(ReadingProgressService progress)
    : EndpointWithoutRequest<ProgressView>
{
    public override void Configure()
    {
        Get("books/{id}/progress");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var bookId = Route<Guid>("id");

        var latest = await progress.GetLatestAsync(bookId, ct);
        if (latest is not null)
        {
            await Send.OkAsync(latest, ct);
            return;
        }

        if (await progress.BookExistsAsync(bookId, ct))
        {
            await Send.NoContentAsync(ct);
            return;
        }

        await Send.NotFoundAsync(ct);
    }
}

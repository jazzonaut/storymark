using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>Permanently deletes a book and all of its data (docs/plan.md §14).
/// Owner-only (ADR-0023): 204 on success, 404 if the book is unknown or not
/// visible, 403 for a public book someone else owns.</summary>
internal sealed class DeleteBookEndpoint(BookDeletionService books)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("books/{id}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var outcome = await books.DeleteAsync(Route<Guid>("id"), User.UserId(), ct);
        if (await outcome.TrySendErrorAsync(HttpContext.Response, ct))
        {
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

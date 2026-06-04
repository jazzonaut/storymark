using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>Removes a bookmark by id. Idempotent — 204 whether or not the
/// bookmark existed (so an offline-queued delete replays safely).</summary>
internal sealed class DeleteBookmarkEndpoint(BookmarkService bookmarks)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("books/{id}/bookmarks/{bookmarkId}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await bookmarks.DeleteAsync(Route<Guid>("id"), User.UserId(), Route<Guid>("bookmarkId"), ct);
        await Send.NoContentAsync(ct);
    }
}

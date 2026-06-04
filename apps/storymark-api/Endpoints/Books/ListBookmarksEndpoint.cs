using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>The reader's bookmarks for a book, ordered by reading position. 404 if the book is unknown.</summary>
internal sealed class ListBookmarksEndpoint(BookmarkService bookmarks)
    : EndpointWithoutRequest<IReadOnlyList<BookmarkView>>
{
    public override void Configure()
    {
        Get("books/{id}/bookmarks");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var list = await bookmarks.ListAsync(Route<Guid>("id"), User.UserId(), ct);
        if (list is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(list, ct);
    }
}

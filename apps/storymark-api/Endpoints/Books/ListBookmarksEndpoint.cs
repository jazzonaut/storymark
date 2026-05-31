using FastEndpoints;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>All bookmarks for a book, ordered by reading position. Public, like
/// the other reader endpoints. 404 if the book is unknown.</summary>
internal sealed class ListBookmarksEndpoint(BookmarkService bookmarks)
    : EndpointWithoutRequest<IReadOnlyList<BookmarkView>>
{
    public override void Configure()
    {
        Get("books/{id}/bookmarks");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var list = await bookmarks.ListAsync(Route<Guid>("id"), ct);
        if (list is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(list, ct);
    }
}

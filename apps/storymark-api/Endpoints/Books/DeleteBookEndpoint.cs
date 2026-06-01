using FastEndpoints;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>Permanently deletes a book and all of its data (docs/plan.md §14).
/// Public, like the other reader endpoints. 204 on success, 404 if the book is
/// unknown.</summary>
internal sealed class DeleteBookEndpoint(BookDeletionService books)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("books/{id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var deleted = await books.DeleteAsync(Route<Guid>("id"), ct);
        if (!deleted)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

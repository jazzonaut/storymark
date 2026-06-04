using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>Lists the reader's library — own books plus public ones — newest
/// upload first.</summary>
internal sealed class ListBooksEndpoint(BookCatalogService catalog)
    : EndpointWithoutRequest<IReadOnlyList<BookSummaryResponse>>
{
    public override void Configure()
    {
        Get("books");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.UserId();
        var entries = await catalog.GetAllAsync(userId, ct);
        var items = entries.Select(e => BookSummaryResponse.From(e, userId)).ToList();
        await Send.OkAsync(items, ct);
    }
}

using FastEndpoints;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>Lists the library, newest upload first. Public (single-user app;
/// reader endpoints are anonymous like <c>/api/health</c>).</summary>
internal sealed class ListBooksEndpoint(BookCatalogService catalog)
    : EndpointWithoutRequest<IReadOnlyList<BookSummaryResponse>>
{
    public override void Configure()
    {
        Get("books");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var books = await catalog.GetAllAsync(ct);
        var items = books.Select(BookSummaryResponse.From).ToList();
        await Send.OkAsync(items, ct);
    }
}

using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

internal sealed record SetVisibilityRequest
{
    public required bool IsPublic { get; init; }
}

/// <summary>Flips a book between private and public (ADR-0023). Owner-only:
/// 404 for a book the caller can't see, 403 for a public book someone else
/// owns. Returns the updated library summary so the card refreshes in place.</summary>
internal sealed class PostVisibilityEndpoint(BookCatalogService catalog)
    : Endpoint<SetVisibilityRequest, BookSummaryResponse>
{
    public override void Configure()
    {
        Post("books/{id}/visibility");
    }

    public override async Task HandleAsync(SetVisibilityRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var result = await catalog.SetVisibilityAsync(Route<Guid>("id"), userId, req.IsPublic, ct);

        if (await result.Outcome.TrySendErrorAsync(HttpContext.Response, ct))
        {
            return;
        }

        // Non-null exactly when the outcome succeeded (BookVisibility contract);
        // a violated invariant should throw here, not silently send nothing.
        var book = result.Book
            ?? throw new InvalidOperationException("SetVisibilityAsync succeeded without a book.");
        await Send.OkAsync(BookSummaryResponse.From(book, userId), ct);
    }
}

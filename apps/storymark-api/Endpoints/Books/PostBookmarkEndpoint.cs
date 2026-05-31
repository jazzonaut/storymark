using FastEndpoints;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

internal sealed record SaveBookmarkRequest
{
    public required Guid Id { get; init; }

    public required int SectionIndex { get; init; }

    public int? ParagraphIndex { get; init; }

    public required int LocationStart { get; init; }

    public string? Label { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Creates or replaces a bookmark (idempotent upsert by client-supplied
/// id). Public. 404 if the book is unknown.</summary>
internal sealed class PostBookmarkEndpoint(BookmarkService bookmarks)
    : Endpoint<SaveBookmarkRequest, BookmarkView>
{
    public override void Configure()
    {
        // The route param is named bookId (not id) so it doesn't bind over the
        // request DTO's own Id (the client-supplied bookmark id).
        Post("books/{bookId}/bookmarks");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SaveBookmarkRequest req, CancellationToken ct)
    {
        var saved = await bookmarks.UpsertAsync(
            Route<Guid>("bookId"),
            new SaveBookmarkInput
            {
                Id = req.Id,
                SectionIndex = req.SectionIndex,
                ParagraphIndex = req.ParagraphIndex,
                LocationStart = req.LocationStart,
                Label = req.Label,
                CreatedAt = req.CreatedAt,
            },
            ct);

        if (saved is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(saved, ct);
    }
}

using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
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
/// id). 404 if the book is unknown; 409 if the id already belongs to another
/// book or another reader (the route book and the session user scope the
/// mutation).</summary>
internal sealed class PostBookmarkEndpoint(BookmarkService bookmarks)
    : Endpoint<SaveBookmarkRequest, BookmarkView>
{
    public override void Configure()
    {
        // The route param is named bookId (not id) so it doesn't bind over the
        // request DTO's own Id (the client-supplied bookmark id).
        Post("books/{bookId}/bookmarks");
    }

    public override async Task HandleAsync(SaveBookmarkRequest req, CancellationToken ct)
    {
        var result = await bookmarks.UpsertAsync(
            Route<Guid>("bookId"),
            User.UserId(),
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

        switch (result.Outcome)
        {
            case BookmarkUpsertOutcome.BookNotFound:
                await Send.NotFoundAsync(ct);
                return;
            case BookmarkUpsertOutcome.IdBelongsToAnotherOwner:
                await Send.StringAsync(
                    "This bookmark id already belongs to another book or reader.", 409, cancellation: ct);
                return;
        }

        if (result.View is { } view)
        {
            await Send.OkAsync(view, ct);
        }
    }
}

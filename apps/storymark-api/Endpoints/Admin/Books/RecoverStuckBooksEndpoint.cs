using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Admin.Books;

internal sealed record RecoverStuckBooksRequest
{
    public int? StaleMinutes { get; init; }
}

internal sealed record RecoverStuckBooksResponse
{
    public required int StaleMinutes { get; init; }

    public required int BooksQueued { get; init; }

    public required IReadOnlyList<Guid> BookIds { get; init; }
}

/// <summary>Recovers books that are still Uploaded after the import should have
/// enqueued processing. This is the manual repair path for a crash between the
/// book-row commit and Hangfire enqueue. Admin-only.</summary>
internal sealed class RecoverStuckBooksEndpoint(BookProcessingAdminService processing)
    : Endpoint<RecoverStuckBooksRequest, RecoverStuckBooksResponse>
{
    private const int DefaultStaleMinutes = 10;
    private const int MaxStaleMinutes = 24 * 60;

    public override void Configure()
    {
        Post("books/recover-stuck");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(RecoverStuckBooksRequest req, CancellationToken ct)
    {
        var staleMinutes = Math.Clamp(req.StaleMinutes ?? DefaultStaleMinutes, 1, MaxStaleMinutes);
        var result = await processing.RecoverStuckUploadsAsync(TimeSpan.FromMinutes(staleMinutes), ct);

        await Send.OkAsync(
            new RecoverStuckBooksResponse
            {
                StaleMinutes = staleMinutes,
                BooksQueued = result.BooksQueued,
                BookIds = result.BookIds,
            },
            ct);
    }
}

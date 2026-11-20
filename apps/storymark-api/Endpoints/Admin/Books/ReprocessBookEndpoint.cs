using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Admin.Books;

internal sealed record ReprocessBookResponse
{
    public required Guid BookId { get; init; }

    public required bool Queued { get; init; }

    public required ProcessingStatus? Status { get; init; }
}

/// <summary>Restarts the full pipeline. An active run requires its ID so a
/// stale page or a retried request cannot cancel a newer run. Admin-only.</summary>
internal sealed class ReprocessBookEndpoint(BookProcessingAdminService processing)
    : EndpointWithoutRequest<ReprocessBookResponse>
{
    public override void Configure()
    {
        Post("books/reprocess/{id}");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var bookId = Route<Guid>("id");
        var result = await processing.ReprocessAsync(bookId, Query<Guid?>("expectedRunId", isRequired: false), ct);
        if (!result.Found)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (!result.Queued)
        {
            await Send.ResultAsync(Results.Json(
                new ReprocessBookResponse { BookId = bookId, Queued = false, Status = result.Status },
                statusCode: StatusCodes.Status409Conflict));
            return;
        }

        await Send.OkAsync(
            new ReprocessBookResponse { BookId = bookId, Queued = true, Status = result.Status },
            ct);
    }
}

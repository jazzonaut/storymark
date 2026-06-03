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

/// <summary>Explicitly re-runs the full processing pipeline for a terminal book.
/// Mid-pipeline books are rejected so a manual action cannot race the existing
/// Hangfire chain. Admin-only.</summary>
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
        var result = await processing.ReprocessAsync(bookId, ct);
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

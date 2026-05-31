using FastEndpoints;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

internal sealed record SaveProgressRequest
{
    public required string DeviceId { get; init; }

    public required int SectionIndex { get; init; }

    public int? ParagraphIndex { get; init; }

    public required int LocationStart { get; init; }

    public required int LocationEnd { get; init; }

    public required DateTimeOffset ClientTimestamp { get; init; }
}

/// <summary>Records a device's reading position and returns the canonical latest
/// across devices (docs/plan.md §12.5). Public, like the other reader endpoints.
/// 404 if the book is unknown.</summary>
internal sealed class PostProgressEndpoint(ReadingProgressService progress)
    : Endpoint<SaveProgressRequest, ProgressView>
{
    public override void Configure()
    {
        Post("books/{id}/progress");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SaveProgressRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.DeviceId))
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var latest = await progress.SaveAsync(
            Route<Guid>("id"),
            new SaveProgressInput
            {
                DeviceId = req.DeviceId,
                SectionIndex = req.SectionIndex,
                ParagraphIndex = req.ParagraphIndex,
                LocationStart = req.LocationStart,
                LocationEnd = req.LocationEnd,
                ClientTimestamp = req.ClientTimestamp,
            },
            ct);

        if (latest is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(latest, ct);
    }
}

using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

internal sealed class UploadBookRequest
{
    public IFormFile? File { get; init; }
}

internal sealed record UploadErrorResponse
{
    public required string Message { get; init; }
}

/// <summary>Uploads an EPUB (docs/plan.md §11.1): validate → SHA-256 → store →
/// dedup → create/reuse book row. Public. The original is streamed to disk; the
/// size cap and signature check happen mid-stream in the file storage service.</summary>
internal sealed class UploadBookEndpoint(BookImportService import)
    : Endpoint<UploadBookRequest, BookSummaryResponse>
{
    public override void Configure()
    {
        Post("books");
        AllowAnonymous();
        AllowFileUploads();
    }

    public override async Task HandleAsync(UploadBookRequest req, CancellationToken ct)
    {
        var file = req.File;
        if (file is null || file.Length == 0)
        {
            await SendErrorAsync(400, "No file was uploaded.");
            return;
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".epub", StringComparison.OrdinalIgnoreCase))
        {
            await SendErrorAsync(400, "Only .epub files are supported.");
            return;
        }

        await using var stream = file.OpenReadStream();
        var outcome = await import.ImportAsync(file.FileName, stream, ct);

        switch (outcome.Error)
        {
            case BookImportError.TooLarge:
                await SendErrorAsync(413, $"File exceeds the maximum upload size of {outcome.MaxBytes} bytes.");
                return;
            case BookImportError.DecompressedTooLarge:
                await SendErrorAsync(413, "EPUB is too large or has too many entries when decompressed.");
                return;
            case BookImportError.NotEpub:
                await SendErrorAsync(400, "File is not a valid EPUB (bad signature).");
                return;
            case BookImportError.Empty:
                await SendErrorAsync(400, "Uploaded file is empty.");
                return;
            case BookImportError.None:
                break;
        }

        if (outcome.Book is null)
        {
            await SendErrorAsync(500, "Import produced no book.");
            return;
        }

        await Send.OkAsync(BookSummaryResponse.From(outcome.Book), ct);
    }

    private async Task SendErrorAsync(int statusCode, string message) =>
        await Send.ResultAsync(Results.Json(new UploadErrorResponse { Message = message }, statusCode: statusCode));
}

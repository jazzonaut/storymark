using System.Net;
using System.Text;
using FastEndpoints;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>Serves a book cover. Until S2.1 extracts real covers, no book has a
/// <c>cover_path</c>, so this returns a generated SVG placeholder.</summary>
internal sealed class GetBookCoverEndpoint(BookCatalogService catalog) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("books/{id}/cover");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var book = await catalog.FindAsync(id, User.UserId(), ct);
        if (book is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (book.CoverPath is not null && File.Exists(book.CoverPath))
        {
            var contentType = ImageContentTypes.ContentTypeForExtension(Path.GetExtension(book.CoverPath))
                ?? "application/octet-stream";
            EpubFileResponse.ApplySandboxHeaders(HttpContext.Response);
            await Send.FileAsync(new FileInfo(book.CoverPath), contentType, cancellation: ct);
            return;
        }

        var placeholder = Encoding.UTF8.GetBytes(PlaceholderSvg(book.Title));
        await Send.BytesAsync(placeholder, contentType: "image/svg+xml", cancellation: ct);
    }

    private static string PlaceholderSvg(string title)
    {
        var initial = title.Trim().Length > 0 ? title.Trim()[0].ToString().ToUpperInvariant() : "?";
        var safe = WebUtility.HtmlEncode(initial);
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="400" height="600" viewBox="0 0 400 600">
              <rect width="400" height="600" fill="#2d2d3a"/>
              <text x="200" y="300" font-family="sans-serif" font-size="160" fill="#9a9ab0" text-anchor="middle" dominant-baseline="central">{safe}</text>
            </svg>
            """;
    }
}

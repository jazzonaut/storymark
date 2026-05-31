using FastEndpoints;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>Serves an inline image resource referenced by a section's sanitised
/// HTML (the rewritten <c>&lt;img src&gt;</c> targets). Public. Path-traversal is
/// guarded in the storage layer; non-image or unknown paths return 404.</summary>
internal sealed class GetBookResourceEndpoint(BookSectionService sections) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("books/{id}/resources/{*path}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var resource = await sections.ResolveResourceAsync(
            Route<Guid>("id"), Route<string>("path") ?? string.Empty, ct);
        if (resource is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Resources are EPUB-supplied bytes. An SVG carrying a script is harmless
        // when loaded via <img> (the only emitter), but the URL is directly
        // navigable — sandbox it and forbid MIME sniffing so a hand-typed resource
        // URL can never execute script in our origin.
        HttpContext.Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        HttpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
        await Send.FileAsync(new FileInfo(resource.FilePath), resource.ContentType, cancellation: ct);
    }
}

using Microsoft.AspNetCore.Http;

namespace Storymark.Api.Endpoints.Books;

/// <summary>Response headers for endpoints that serve EPUB-supplied file bytes (the
/// cover and inline resources). Those bytes are untrusted — an SVG can carry a script.
/// The app only ever loads them via <c>&lt;img&gt;</c>, where that's inert, but the URLs
/// are directly navigable, so sandbox them and forbid MIME sniffing: a hand-typed cover
/// or resource URL can never execute script in our origin.</summary>
internal static class EpubFileResponse
{
    public static void ApplySandboxHeaders(HttpResponse response)
    {
        response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        response.Headers["X-Content-Type-Options"] = "nosniff";
    }
}

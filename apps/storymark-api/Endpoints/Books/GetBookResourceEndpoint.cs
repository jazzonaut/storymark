using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>Serves an inline image resource referenced by a section's sanitised
/// HTML (the rewritten <c>&lt;img src&gt;</c> targets). Path-traversal is
/// guarded in the storage layer; non-image or unknown paths return 404.</summary>
internal sealed class GetBookResourceEndpoint(BookSectionService sections) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("books/{id}/resources/{*path}");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var resource = await sections.ResolveResourceAsync(
            Route<Guid>("id"), User.UserId(), Route<string>("path") ?? string.Empty, ct);
        if (resource is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        EpubFileResponse.ApplySandboxHeaders(HttpContext.Response);
        await Send.FileAsync(new FileInfo(resource.FilePath), resource.ContentType, cancellation: ct);
    }
}

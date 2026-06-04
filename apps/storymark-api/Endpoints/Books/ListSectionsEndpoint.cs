using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>Section manifest for a book (docs/plan.md §11.3). 404 if the book is unknown.</summary>
internal sealed class ListSectionsEndpoint(BookSectionService sections)
    : EndpointWithoutRequest<IReadOnlyList<SectionSummaryView>>
{
    public override void Configure()
    {
        Get("books/{id}/sections");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var manifest = await sections.GetManifestAsync(Route<Guid>("id"), User.UserId(), ct);
        if (manifest is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(manifest, ct);
    }
}

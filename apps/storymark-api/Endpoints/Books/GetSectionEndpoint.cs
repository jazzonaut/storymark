using FastEndpoints;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>Full content for one section: sanitised HTML + paragraph offsets
/// (docs/plan.md §11.3). Public. 404 if the book or section is unknown.</summary>
internal sealed class GetSectionEndpoint(BookSectionService sections)
    : EndpointWithoutRequest<SectionContentView>
{
    public override void Configure()
    {
        Get("books/{id}/sections/{idx}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var content = await sections.GetSectionAsync(Route<Guid>("id"), Route<int>("idx"), ct);
        if (content is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(content, ct);
    }
}

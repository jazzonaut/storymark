using FastEndpoints;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>Generates a spoiler-safe "current scene" card at the reader's exact
/// position. Public, like other reader endpoints. 404 if the book is unknown.
/// <c>section</c>/<c>location</c> are required because they are the spoiler boundary.</summary>
internal sealed class GetCurrentSceneEndpoint(CurrentSceneService scene)
    : EndpointWithoutRequest<CurrentSceneView>
{
    public override void Configure()
    {
        Get("books/{id}/scene");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await scene.GetAsync(
            Route<Guid>("id"),
            Query<int>("section", isRequired: true),
            Query<int>("location", isRequired: true),
            ct);

        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}

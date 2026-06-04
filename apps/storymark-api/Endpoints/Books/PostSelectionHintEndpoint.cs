using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Selection;

namespace Storymark.Api.Endpoints.Books;

internal sealed record SelectionHintRequest
{
    public required string SelectedText { get; init; }

    public required int SectionIndex { get; init; }

    public required int LocationStart { get; init; }

    public required int LocationEnd { get; init; }

    public required int CurrentLocation { get; init; }
}

internal sealed record SelectionHintResponse
{
    public required string Kind { get; init; }

    public Guid? EntityId { get; init; }

    public EntityCard? Card { get; init; }

    public string? Hint { get; init; }

    public IReadOnlyList<HintSource> Sources { get; init; } = [];
}

/// <summary>Resolves a text selection to an instant entity card or a fast 1B
/// hint, using only content the reader has already passed (docs/plan.md §5.5,
/// §11.7). 400 on a blank selection,
/// 404 if the book is unknown.</summary>
internal sealed class PostSelectionHintEndpoint(SelectionHintService selection)
    : Endpoint<SelectionHintRequest, SelectionHintResponse>
{
    public override void Configure()
    {
        Post("books/{id}/selection/hint");
    }

    public override async Task HandleAsync(SelectionHintRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.SelectedText))
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var result = await selection.GetHintAsync(
            Route<Guid>("id"),
            User.UserId(),
            req.SelectedText,
            req.SectionIndex,
            req.CurrentLocation,
            ct);

        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(
            new SelectionHintResponse
            {
                Kind = result.Kind == SelectionHintKind.Entity ? "entity" : "hint",
                EntityId = result.EntityId,
                Card = result.Card,
                Hint = result.Hint,
                Sources = result.Sources,
            },
            ct);
    }
}

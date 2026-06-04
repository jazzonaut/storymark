using FastEndpoints;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Assistant;

namespace Storymark.Api.Endpoints.Books;

internal sealed record AskRequest
{
    public required string Question { get; init; }

    public string? SelectedText { get; init; }

    public IReadOnlyList<AskHistoryTurn>? History { get; init; }

    public required int CurrentSection { get; init; }

    public required int CurrentLocation { get; init; }
}

internal sealed record AskHistoryTurn
{
    public required string Question { get; init; }

    public required string Answer { get; init; }
}

internal sealed record AskResponse
{
    public required string Answer { get; init; }

    public required string Confidence { get; init; }

    public required IReadOnlyList<QuestionSource> Sources { get; init; }

    public required bool UsedFutureContent { get; init; }
}

/// <summary>Answers a question about the book using only content the reader has
/// already passed (docs/plan.md §11.6, §18). The optional selection and history
/// make a turn conversational while staying stateless server-side — the client
/// carries the thread (docs/adr/0021).
/// 400 on a blank question, 404 if the book is unknown.</summary>
internal sealed class PostAskEndpoint(AssistantService assistant)
    : Endpoint<AskRequest, AskResponse>
{
    public override void Configure()
    {
        Post("books/{id}/ask");
    }

    public override async Task HandleAsync(AskRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Question))
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var answer = await assistant.AnswerAsync(
            Route<Guid>("id"),
            User.UserId(),
            new AssistantQuestion
            {
                Question = req.Question.Trim(),
                SelectedText = req.SelectedText,
                History = req.History
                    ?.Select(t => new AssistantHistoryTurn { Question = t.Question, Answer = t.Answer })
                    .ToList() ?? [],
                CurrentSection = req.CurrentSection,
                CurrentLocation = req.CurrentLocation,
            },
            ct);

        if (answer is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(
            new AskResponse
            {
                Answer = answer.Answer,
                Confidence = answer.Confidence.ToWireValue(),
                Sources = answer.Sources,
                UsedFutureContent = answer.UsedFutureContent,
            },
            ct);
    }
}

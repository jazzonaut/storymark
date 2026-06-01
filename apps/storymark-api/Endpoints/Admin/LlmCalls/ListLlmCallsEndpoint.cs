using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Llm;

namespace Storymark.Api.Endpoints.Admin.LlmCalls;

internal sealed record LlmCallItem
{
    public required Guid Id { get; init; }

    public required string Task { get; init; }

    public required string Provider { get; init; }

    public required string Model { get; init; }

    public required string Prompt { get; init; }

    public string? Response { get; init; }

    public int? TokensIn { get; init; }

    public int? TokensOut { get; init; }

    public int? LatencyMs { get; init; }

    public decimal? CostEstimate { get; init; }

    public Guid? BookId { get; init; }

    public int? SectionIndex { get; init; }

    public required bool UsedFutureContent { get; init; }

    public required bool Succeeded { get; init; }

    public string? Error { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Lists recent LLM calls, newest first, optionally filtered by task.
/// Admin-only. <c>limit</c> defaults to 50 and is capped at 200.</summary>
internal sealed class ListLlmCallsEndpoint(LlmCallLogService log)
    : EndpointWithoutRequest<IReadOnlyList<LlmCallItem>>
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    public override void Configure()
    {
        Get("llm-calls");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var task = Query<string?>("task", isRequired: false);
        var limit = Math.Clamp(Query<int?>("limit", isRequired: false) ?? DefaultLimit, 1, MaxLimit);

        var calls = await log.GetRecentAsync(task, limit, ct);

        var items = calls
            .Select(c => new LlmCallItem
            {
                Id = c.Id,
                Task = c.Task,
                Provider = c.Provider,
                Model = c.Model,
                Prompt = c.Prompt,
                Response = c.Response,
                TokensIn = c.TokensIn,
                TokensOut = c.TokensOut,
                LatencyMs = c.LatencyMs,
                CostEstimate = c.CostEstimate,
                BookId = c.BookId,
                SectionIndex = c.SectionIndex,
                UsedFutureContent = c.UsedFutureContent,
                Succeeded = c.Succeeded,
                Error = c.Error,
                CreatedAt = c.CreatedAt,
            })
            .ToList();

        await Send.OkAsync(items, ct);
    }
}

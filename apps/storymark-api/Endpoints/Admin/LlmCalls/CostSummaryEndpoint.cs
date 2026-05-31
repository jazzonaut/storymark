using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Llm;

namespace Storymark.Api.Endpoints.Admin.LlmCalls;

internal sealed record TaskCostItem
{
    public required string Task { get; init; }

    public required int CallCount { get; init; }

    public long? TokensIn { get; init; }

    public long? TokensOut { get; init; }

    public decimal? CostEstimate { get; init; }
}

internal sealed record CostSummaryResponse
{
    public required int WindowDays { get; init; }

    public required int CallCount { get; init; }

    public long? TokensIn { get; init; }

    public long? TokensOut { get; init; }

    public decimal? CostEstimate { get; init; }

    public required IReadOnlyList<TaskCostItem> Tasks { get; init; }
}

/// <summary>Cost and token totals grouped by task over a fixed 30-day window,
/// plus grand totals. Admin-only. Feeds the cost-meter chart.</summary>
internal sealed class CostSummaryEndpoint(LlmCallLogService log)
    : EndpointWithoutRequest<CostSummaryResponse>
{
    private const int WindowDays = 30;

    public override void Configure()
    {
        Get("llm-calls/cost-summary");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tasks = await log.GetCostSummaryAsync(WindowDays, ct);

        var anyCost = tasks.Any(t => t.CostEstimate.HasValue);

        var response = new CostSummaryResponse
        {
            WindowDays = WindowDays,
            CallCount = tasks.Sum(t => t.CallCount),
            TokensIn = tasks.Sum(t => t.TokensIn ?? 0L),
            TokensOut = tasks.Sum(t => t.TokensOut ?? 0L),
            CostEstimate = anyCost ? tasks.Sum(t => t.CostEstimate ?? 0m) : null,
            Tasks = tasks
                .Select(t => new TaskCostItem
                {
                    Task = t.Task,
                    CallCount = t.CallCount,
                    TokensIn = t.TokensIn,
                    TokensOut = t.TokensOut,
                    CostEstimate = t.CostEstimate,
                })
                .ToList(),
        };

        await Send.OkAsync(response, ct);
    }
}

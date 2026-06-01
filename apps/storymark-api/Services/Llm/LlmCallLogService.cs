using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.LlmCalls;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Services.Llm;

/// <summary>Per-task spend over the cost-meter window. Tokens are summed as
/// <c>long</c> to avoid 32-bit overflow; <see cref="CostEstimate"/> is null when
/// no row in the group has a cost (e.g. a local model with no configured rate).</summary>
internal sealed record LlmTaskCost
{
    public required string Task { get; init; }

    public required int CallCount { get; init; }

    public long? TokensIn { get; init; }

    public long? TokensOut { get; init; }

    public decimal? CostEstimate { get; init; }
}

/// <summary>Read access to the LLM call log for the admin call-log and cost-meter
/// views. All aggregation lives here, not in the endpoints (layering rule).</summary>
internal sealed class LlmCallLogService(StorymarkDbContext db)
{
    public async Task<IReadOnlyList<LlmCall>> GetRecentAsync(string? task, int limit, CancellationToken ct)
    {
        var query = db.LlmCalls.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(task))
        {
            query = query.Where(c => c.Task == task);
        }

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    /// <summary>Cost and token totals grouped by task over the last
    /// <paramref name="windowDays"/> days, most expensive task first. SQL
    /// <c>SUM</c> yields NULL for a group whose costs are all NULL, so an
    /// unpriced task surfaces a null cost rather than a misleading zero.</summary>
    public async Task<IReadOnlyList<LlmTaskCost>> GetCostSummaryAsync(int windowDays, CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-windowDays);

        // EF translates Sum over a nullable to COALESCE(SUM(x), 0), so an unpriced
        // group would surface a misleading 0 instead of "no cost". Carry the count
        // of priced rows so an all-unpriced group collapses back to null.
        var grouped = await db.LlmCalls
            .AsNoTracking()
            .Where(c => c.CreatedAt >= since && c.Succeeded)
            .GroupBy(c => c.Task)
            .Select(g => new
            {
                Task = g.Key,
                CallCount = g.Count(),
                TokensIn = g.Sum(c => (long?)c.TokensIn),
                TokensOut = g.Sum(c => (long?)c.TokensOut),
                Cost = g.Sum(c => c.CostEstimate),
                PricedCount = g.Count(c => c.CostEstimate != null),
            })
            .ToListAsync(ct);

        return grouped
            .Select(g => new LlmTaskCost
            {
                Task = g.Task,
                CallCount = g.CallCount,
                TokensIn = g.TokensIn,
                TokensOut = g.TokensOut,
                CostEstimate = g.PricedCount > 0 ? g.Cost : null,
            })
            .OrderByDescending(s => s.CostEstimate ?? 0m)
            .ThenByDescending(s => s.CallCount)
            .ToList();
    }
}

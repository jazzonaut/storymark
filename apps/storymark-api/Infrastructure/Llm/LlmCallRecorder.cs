using System.Diagnostics;
using Storymark.Api.Domain.LlmCalls;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Infrastructure.Llm;

/// <summary>The single persistence chokepoint every LLM call routes through:
/// times the invocation, estimates cost from <c>llm.costRates</c>, and writes
/// one <c>llm_calls</c> row per successful call. A failed invocation propagates
/// without a row (auditing failed attempts needs a status column — deferred).
/// The spoiler-safety audit (CLAUDE.md, docs/plan.md §17) depends on no call
/// site bypassing this.</summary>
internal sealed class LlmCallRecorder(
    StorymarkDbContext db,
    ISettingsService settings,
    ILogger<LlmCallRecorder> logger)
{
    public async Task<T> RecordAsync<T>(
        LlmCallDescriptor descriptor,
        Func<CancellationToken, Task<LlmCallResult<T>>> invoke,
        CancellationToken ct)
    {
        var createdAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var result = await invoke(ct);
        stopwatch.Stop();

        db.LlmCalls.Add(new LlmCall
        {
            Id = Guid.NewGuid(),
            Task = descriptor.Task,
            Provider = descriptor.Provider,
            Model = descriptor.Model,
            Prompt = descriptor.Prompt,
            Response = result.ResponseText,
            TokensIn = result.TokensIn,
            TokensOut = result.TokensOut,
            LatencyMs = (int)stopwatch.ElapsedMilliseconds,
            CostEstimate = EstimateCost(descriptor, result.TokensIn, result.TokensOut),
            BookId = descriptor.BookId,
            SectionIndex = descriptor.SectionIndex,
            UsedFutureContent = descriptor.UsedFutureContent,
            CreatedAt = createdAt,
        });
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Recorded {Task} call to {Provider}:{Model} in {LatencyMs}ms",
            descriptor.Task,
            descriptor.Provider,
            descriptor.Model,
            stopwatch.ElapsedMilliseconds);

        return result.Value;
    }

    private decimal? EstimateCost(LlmCallDescriptor descriptor, int? tokensIn, int? tokensOut)
    {
        var rates = settings.GetJson<Dictionary<string, LlmCostRate>>("llm.costRates");
        if (!rates.TryGetValue($"{descriptor.Provider}:{descriptor.Model}", out var rate))
        {
            return null;
        }

        if (tokensIn is null && tokensOut is null)
        {
            return null;
        }

        var input = (tokensIn ?? 0) / 1_000_000m * rate.InputPerMillion;
        var output = (tokensOut ?? 0) / 1_000_000m * rate.OutputPerMillion;
        return input + output;
    }
}

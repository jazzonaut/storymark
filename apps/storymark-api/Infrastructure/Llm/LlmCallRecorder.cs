using System.Diagnostics;
using Storymark.Api.Domain.LlmCalls;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Infrastructure.Llm;

/// <summary>The single persistence chokepoint every LLM call routes through:
/// times the invocation, estimates cost from <c>llm.costRates</c>, and writes
/// one <c>llm_calls</c> row per call — successful or failed. A failed invocation
/// records a row with <c>Succeeded = false</c> and the error, then rethrows, so
/// failures surface in the admin call log instead of vanishing. The spoiler-safety
/// audit (CLAUDE.md, docs/plan.md §17) depends on no call site bypassing this.</summary>
internal sealed class LlmCallRecorder(
    StorymarkDbContext db,
    ILogger<LlmCallRecorder> logger)
{
    public async Task<T> RecordAsync<T>(
        LlmCallDescriptor descriptor,
        Func<CancellationToken, Task<LlmCallResult<T>>> invoke,
        CancellationToken ct)
    {
        var createdAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        LlmCallResult<T> result;
        try
        {
            result = await invoke(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await RecordFailureAsync(descriptor, ex, (int)stopwatch.ElapsedMilliseconds, createdAt);
            throw;
        }

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

    private async Task RecordFailureAsync(
        LlmCallDescriptor descriptor, Exception ex, int latencyMs, DateTimeOffset createdAt)
    {
        db.LlmCalls.Add(new LlmCall
        {
            Id = Guid.NewGuid(),
            Task = descriptor.Task,
            Provider = descriptor.Provider,
            Model = descriptor.Model,
            Prompt = descriptor.Prompt,
            Response = null,
            TokensIn = null,
            TokensOut = null,
            LatencyMs = latencyMs,
            CostEstimate = null,
            BookId = descriptor.BookId,
            SectionIndex = descriptor.SectionIndex,
            UsedFutureContent = descriptor.UsedFutureContent,
            Succeeded = false,
            Error = Truncate(ex.Message, 2000),
            CreatedAt = createdAt,
        });

        // The triggering call already failed; persist the audit row regardless of
        // the original token so the failure isn't lost on a cancelled request.
        await db.SaveChangesAsync(CancellationToken.None);

        logger.LogWarning(
            ex, "LLM {Task} call to {Provider}:{Model} failed after {LatencyMs}ms",
            descriptor.Task, descriptor.Provider, descriptor.Model, latencyMs);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static decimal? EstimateCost(LlmCallDescriptor descriptor, int? tokensIn, int? tokensOut)
    {
        if (descriptor.InputPerMillion is null && descriptor.OutputPerMillion is null)
        {
            return null;
        }

        if (tokensIn is null && tokensOut is null)
        {
            return null;
        }

        var input = (tokensIn ?? 0) / 1_000_000m * (descriptor.InputPerMillion ?? 0m);
        var output = (tokensOut ?? 0) / 1_000_000m * (descriptor.OutputPerMillion ?? 0m);
        return input + output;
    }
}

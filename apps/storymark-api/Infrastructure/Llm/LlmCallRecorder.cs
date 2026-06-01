using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Storymark.Api.Domain.LlmCalls;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Infrastructure.Llm;

/// <summary>The single persistence chokepoint every LLM call routes through:
/// times the invocation, estimates cost from the model's rate, and writes one
/// <c>llm_calls</c> row per call — successful or failed. A failed invocation
/// records a row with <c>Succeeded = false</c> and the error, then rethrows, so
/// failures surface in the admin call log instead of vanishing. The spoiler-safety
/// audit (CLAUDE.md, docs/plan.md §17) depends on no call site bypassing this.
///
/// <para>The audit row is written through a <em>dedicated</em> short-lived
/// <see cref="StorymarkDbContext"/> resolved from a child scope, not the caller's
/// scoped context. Recording a call must never flush the caller's other pending
/// changes (a shared <c>SaveChanges</c> is unit-of-work-wide), and a failed call's
/// audit row must persist even when the caller's work is being abandoned.</para></summary>
internal sealed class LlmCallRecorder(
    IServiceScopeFactory scopeFactory,
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

        var call = new LlmCall
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
        };
        await PersistAsync(call, ct);

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
        var call = new LlmCall
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
        };

        // The triggering call already failed; persist the audit row with a fresh token
        // so the failure isn't lost when the original request is being cancelled.
        await PersistAsync(call, CancellationToken.None);

        logger.LogWarning(
            ex, "LLM {Task} call to {Provider}:{Model} failed after {LatencyMs}ms",
            descriptor.Task, descriptor.Provider, descriptor.Model, latencyMs);
    }

    private async Task PersistAsync(LlmCall call, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StorymarkDbContext>();
        db.LlmCalls.Add(call);
        await db.SaveChangesAsync(ct);
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

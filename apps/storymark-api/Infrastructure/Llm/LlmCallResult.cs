namespace Storymark.Api.Infrastructure.Llm;

/// <summary>The outcome of an invocation wrapped by <see cref="LlmCallRecorder"/>:
/// the typed value returned to the caller plus the fields persisted to the log.</summary>
internal sealed record LlmCallResult<T>
{
    public required T Value { get; init; }

    public string? ResponseText { get; init; }

    public int? TokensIn { get; init; }

    public int? TokensOut { get; init; }
}

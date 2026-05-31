using Storymark.Api.Domain.Books;

namespace Storymark.Api.Services.Assistant;

/// <summary>The result of a spoiler-safe Q&amp;A turn: the model's answer, a coarse
/// confidence derived from the evidence, the sources it cited, and the audit flag
/// (always <c>false</c> by construction — see <see cref="AssistantService"/>).</summary>
internal sealed record AssistantAnswer
{
    public required string Answer { get; init; }

    public required AnswerConfidence Confidence { get; init; }

    public required IReadOnlyList<QuestionSource> Sources { get; init; }

    public required bool UsedFutureContent { get; init; }
}

internal enum AnswerConfidence
{
    Low,
    Medium,
    High,
}

internal static class AnswerConfidenceExtensions
{
    public static string ToWireValue(this AnswerConfidence confidence) => confidence switch
    {
        AnswerConfidence.Low => "low",
        AnswerConfidence.Medium => "medium",
        AnswerConfidence.High => "high",
        _ => throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Unknown confidence."),
    };
}

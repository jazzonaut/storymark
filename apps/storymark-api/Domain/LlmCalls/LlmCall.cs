namespace Storymark.Api.Domain.LlmCalls;

/// <summary>One persisted LLM invocation. Powers the admin call log and cost
/// meter and is the spoiler-safety audit trail (docs/plan.md §12.16, §20.2).</summary>
internal sealed class LlmCall
{
    public Guid Id { get; set; }

    public required string Task { get; set; }

    public required string Provider { get; set; }

    public required string Model { get; set; }

    public required string Prompt { get; set; }

    public string? Response { get; set; }

    public int? TokensIn { get; set; }

    public int? TokensOut { get; set; }

    public int? LatencyMs { get; set; }

    public decimal? CostEstimate { get; set; }

    public Guid? BookId { get; set; }

    public int? SectionIndex { get; set; }

    public bool UsedFutureContent { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

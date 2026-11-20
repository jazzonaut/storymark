namespace Storymark.Api.Domain.Llm;

/// <summary>A model offered by a <see cref="LlmProvider"/> (ADR-0016). Tasks
/// reference a model by <see cref="Id"/>; per-million-token pricing lives here and
/// drives the cost meter (replacing the old <c>llm.costRates</c> setting).</summary>
internal sealed class LlmModel
{
    public Guid Id { get; set; }

    public required string Label { get; set; }

    public Guid ProviderId { get; set; }

    /// <summary>The provider's wire name for the model (e.g. <c>llama3.2:3b</c>,
    /// <c>openai/gpt-6-luna</c>).</summary>
    public required string ModelName { get; set; }

    /// <summary>Null uses the provider default; "none" disables reasoning.</summary>
    public string? ReasoningEffort { get; set; }

    public decimal? InputPerMillion { get; set; }

    public decimal? OutputPerMillion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

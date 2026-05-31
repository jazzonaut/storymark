namespace Storymark.Api.Infrastructure.Llm;

/// <summary>Per-million-token pricing for a hosted <c>provider:model</c>, read
/// from the <c>llm.costRates</c> setting.</summary>
internal sealed record LlmCostRate
{
    public decimal InputPerMillion { get; init; }

    public decimal OutputPerMillion { get; init; }
}

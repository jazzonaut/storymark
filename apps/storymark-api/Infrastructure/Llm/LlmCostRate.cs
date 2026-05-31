using System.Text.Json.Serialization;

namespace Storymark.Api.Infrastructure.Llm;

/// <summary>Per-million-token pricing for a hosted <c>provider:model</c>, read
/// from the <c>llm.costRates</c> setting. Property names are pinned to camelCase
/// so the JSON contract matches the rest of the wire format an operator edits in
/// the admin settings panel; default (case-sensitive) deserialization would
/// otherwise silently zero a rate written as <c>inputPerMillion</c>.</summary>
internal sealed record LlmCostRate
{
    [JsonPropertyName("inputPerMillion")]
    public decimal InputPerMillion { get; init; }

    [JsonPropertyName("outputPerMillion")]
    public decimal OutputPerMillion { get; init; }
}

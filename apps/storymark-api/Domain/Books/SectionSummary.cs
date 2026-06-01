namespace Storymark.Api.Domain.Books;

/// <summary>The per-section output of <c>SectionExtractionJob</c> (docs/plan.md
/// §12.7, §15.2): a short summary plus the section's extracted events, the entity
/// names seen in it, and unresolved threads. The events/entities/threads are a
/// denormalised snapshot of the extraction for the recap/memory pass (S5.2); the
/// queryable forms live in <see cref="BookEvent"/>/<see cref="BookEntity"/>.</summary>
internal sealed class SectionSummary
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    public int SectionIndex { get; set; }

    public required string Summary { get; set; }

    public required IReadOnlyList<SectionSummaryEvent> ImportantEvents { get; set; }

    public required IReadOnlyList<string> EntitiesSeen { get; set; }

    public required IReadOnlyList<string> OpenThreads { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }
}

/// <summary>An event as the model reported it for a section, kept verbatim in the
/// summary snapshot. <see cref="InvolvedEntityNames"/> are the raw names (the
/// normalised <see cref="BookEvent"/> row resolves these to entity ids).</summary>
internal sealed record SectionSummaryEvent
{
    public required string Description { get; init; }

    public string? EventType { get; init; }

    public required int LocationStart { get; init; }

    public required int LocationEnd { get; init; }

    public required IReadOnlyList<string> InvolvedEntityNames { get; init; }
}

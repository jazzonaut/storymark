namespace Storymark.Api.Services.Extraction;

/// <summary>The combined per-section extraction the model returns as JSON
/// (docs/plan.md §19.2). Properties are deserialised case-insensitively from the
/// prompt's camelCase shape; lists are nullable because a model may omit empty
/// arrays, and are coalesced at the persistence seam.</summary>
internal sealed record SectionExtraction
{
    public string? ShortSummary { get; init; }

    public IReadOnlyList<ExtractedEvent>? ImportantEvents { get; init; }

    public IReadOnlyList<ExtractedEntity>? EntitiesIntroduced { get; init; }

    public IReadOnlyList<ExtractedRelationship>? RelationshipDeltas { get; init; }

    public IReadOnlyList<string>? OpenThreads { get; init; }
}

internal sealed record ExtractedEvent
{
    public string? Description { get; init; }

    public string? EventType { get; init; }

    public int LocationStart { get; init; }

    public int LocationEnd { get; init; }

    public IReadOnlyList<string>? InvolvedEntityNames { get; init; }
}

internal sealed record ExtractedEntity
{
    public string? CanonicalName { get; init; }

    public string? EntityType { get; init; }

    public IReadOnlyList<string>? Aliases { get; init; }

    public string? FirstSeenEvidence { get; init; }
}

internal sealed record ExtractedRelationship
{
    public string? FromName { get; init; }

    public string? ToName { get; init; }

    public string? Type { get; init; }

    public string? Polarity { get; init; }

    public string? Evidence { get; init; }
}

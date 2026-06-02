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

    /// <summary>A short verbatim quote from the section where the event occurs. The
    /// event is positioned by locating this in the section text (small models can't
    /// produce reliable character offsets); an event whose quote can't be found is
    /// anchored to the section end so it never reveals before it has been read.</summary>
    public string? Evidence { get; init; }

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

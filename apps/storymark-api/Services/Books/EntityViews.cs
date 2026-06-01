namespace Storymark.Api.Services.Books;

/// <summary>A visible entity for in-text highlighting, carried on a section payload
/// (<c>GET /api/books/{id}/sections/{idx}</c>) and the Story Guide list. Only entities
/// the reader has reached (<c>first_seen_section ≤</c> the section) are included.</summary>
internal sealed record SectionEntityView
{
    public required Guid EntityId { get; init; }

    public required string CanonicalName { get; init; }

    public required string EntityType { get; init; }

    public required IReadOnlyList<string> Aliases { get; init; }

    public required int? FirstSeenSection { get; init; }
}

/// <summary>A Story Guide list entry (<c>GET /api/books/{id}/entities</c>): a visible
/// entity plus its latest cached short summary at-or-before the position, if one has
/// been generated yet (else <c>null</c> — tapping it generates one).</summary>
internal sealed record StoryGuideEntityView
{
    public required Guid EntityId { get; init; }

    public required string CanonicalName { get; init; }

    public required string EntityType { get; init; }

    public required IReadOnlyList<string> Aliases { get; init; }

    public required int? FirstSeenSection { get; init; }

    public string? ShortSummary { get; init; }
}

/// <summary>A spoiler-safe entity summary at a reading position
/// (<c>GET /api/books/{id}/entities/{entityId}/summary</c>).</summary>
internal sealed record EntitySummaryView
{
    public required string CanonicalName { get; init; }

    public required string EntityType { get; init; }

    public required string ShortSummary { get; init; }

    public required IReadOnlyList<string> KnownFacts { get; init; }

    public required IReadOnlyList<string> Relationships { get; init; }

    public required IReadOnlyList<string> OpenQuestions { get; init; }
}

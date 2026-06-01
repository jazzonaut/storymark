namespace Storymark.Api.Services.Books;

/// <summary>An extracted event visible at a reading position
/// (<c>GET /api/books/{id}/events</c>). Offsets index the section's plain text;
/// the timeline navigates to <see cref="LocationStart"/>.</summary>
internal sealed record EventView
{
    public required int SectionIndex { get; init; }

    public required int LocationStart { get; init; }

    public required int LocationEnd { get; init; }

    public string? EventType { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<Guid> InvolvedEntityIds { get; init; }
}

/// <summary>An extracted relationship visible at a reading position
/// (<c>GET /api/books/{id}/relationships</c>). Endpoints reference reconciled
/// <c>book_entities</c> ids.</summary>
internal sealed record RelationshipView
{
    public required Guid FromEntityId { get; init; }

    public required Guid ToEntityId { get; init; }

    public required string RelationshipType { get; init; }

    public string? Polarity { get; init; }

    public string? Evidence { get; init; }

    public required int FirstSeenSection { get; init; }

    public required int FirstSeenLocation { get; init; }
}

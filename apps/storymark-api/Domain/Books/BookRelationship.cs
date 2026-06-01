namespace Storymark.Api.Domain.Books;

/// <summary>A directed relationship between two reconciled entities, first seen at a
/// position (docs/plan.md §12.13). Surfaced on the relationship graph, filtered to
/// relationships first seen at-or-before the reader's position. An unresolved
/// endpoint name in extraction lazily creates an <see cref="EntityType.Unknown"/>
/// entity rather than dropping the edge.</summary>
internal sealed class BookRelationship
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    public Guid FromEntityId { get; set; }

    public Guid ToEntityId { get; set; }

    public required string RelationshipType { get; set; }

    /// <summary>positive | negative | neutral | complex, or <c>null</c>.</summary>
    public string? Polarity { get; set; }

    /// <summary>A short supporting quote/snippet from the section.</summary>
    public string? Evidence { get; set; }

    public int FirstSeenSection { get; set; }

    public int FirstSeenLocation { get; set; }

    public int? LastSeenSection { get; set; }

    public int? LastSeenLocation { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

namespace Storymark.Api.Domain.Books;

/// <summary>A Story Guide entity — a recurring person/place/thing in a book
/// (docs/plan.md §12.8). Created here (S4.3) for the selection-hint entity
/// short-circuit; populated by the section-extraction pipeline from S5.1.
/// <see cref="FirstSeenSection"/>/<see cref="FirstSeenLocation"/> gate
/// spoiler-safe visibility — an entity is only surfaced once the reader has
/// reached where it is introduced.</summary>
internal sealed class BookEntity
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    public required string CanonicalName { get; set; }

    public EntityType EntityType { get; set; }

    public required IReadOnlyList<string> Aliases { get; set; }

    public int? FirstSeenSection { get; set; }

    public int? FirstSeenLocation { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

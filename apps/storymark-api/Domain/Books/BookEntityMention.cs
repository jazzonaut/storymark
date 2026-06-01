namespace Storymark.Api.Domain.Books;

/// <summary>A located occurrence of an entity within a section (docs/plan.md §12.9).
/// Extraction records one mention per entity at the position of each event it is
/// involved in; later slices (S6.1's entity summary) read the visible mentions to
/// build a spoiler-safe picture of the entity. Offsets index the section's plain
/// text — the shared coordinate space.</summary>
internal sealed class BookEntityMention
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    public Guid EntityId { get; set; }

    public int SectionIndex { get; set; }

    public int LocationStart { get; set; }

    public int LocationEnd { get; set; }

    public required string MentionText { get; set; }

    public string? SurroundingText { get; set; }

    public decimal Confidence { get; set; }
}

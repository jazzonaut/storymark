namespace Storymark.Api.Domain.Books;

/// <summary>A plot event extracted from a section (docs/plan.md §12.12), positioned
/// in the section's plain-text coordinate space. <see cref="InvolvedEntityIds"/>
/// references reconciled <see cref="BookEntity"/> rows. Surfaced on the timeline,
/// filtered to events whose position the reader has already passed.</summary>
internal sealed class BookEvent
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    public int SectionIndex { get; set; }

    public int LocationStart { get; set; }

    public int LocationEnd { get; set; }

    /// <summary>A coarse classification (arrival, death, reveal, …) or <c>null</c>
    /// when the model didn't classify it.</summary>
    public string? EventType { get; set; }

    public required string Description { get; set; }

    public required IReadOnlyList<Guid> InvolvedEntityIds { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

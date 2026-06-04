namespace Storymark.Api.Domain.Books;

/// <summary>A persisted free-text Q&amp;A turn against a book at a reading position
/// (docs/plan.md §12.14). The answer is grounded only in spoiler-safe evidence;
/// <see cref="Sources"/> records the chunks it was built from.</summary>
internal sealed class QuestionAnswer
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    /// <summary>The reader who asked.</summary>
    public Guid UserId { get; set; }

    public required string Question { get; set; }

    public required string Answer { get; set; }

    public int CurrentSection { get; set; }

    public int CurrentLocation { get; set; }

    public required IReadOnlyList<QuestionSource> Sources { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>One evidence chunk an answer cited, with a short snippet for display.
/// <see cref="Index"/> is the 1-based citation number the answer text refers to
/// inline (e.g. <c>[2]</c>). Offsets index the section's plain text (the shared
/// coordinate space).</summary>
internal sealed record QuestionSource
{
    public required int Index { get; init; }

    public required int SectionIndex { get; init; }

    public required int LocationStart { get; init; }

    public required int LocationEnd { get; init; }

    public required string Snippet { get; init; }
}

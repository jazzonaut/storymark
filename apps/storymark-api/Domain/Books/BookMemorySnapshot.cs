namespace Storymark.Api.Domain.Books;

/// <summary>A rolling "story so far" composed at a section boundary (docs/plan.md
/// §12.11, §15.2). Built incrementally — each snapshot folds the previous one and the
/// next section summary into an updated <see cref="SummarySoFar"/> — so the
/// section-entry recap card (S5.3) renders with zero read-time LLM latency.
/// <see cref="UpToSection"/>/<see cref="UpToLocation"/> mark the boundary (end of that
/// section); the recap reads the latest snapshot at-or-before the reading position, so
/// nothing beyond it leaks.</summary>
internal sealed class BookMemorySnapshot
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    public int UpToSection { get; set; }

    public int UpToLocation { get; set; }

    public required string SummarySoFar { get; set; }

    public required IReadOnlyList<string> Timeline { get; set; }

    public required IReadOnlyList<string> OpenThreads { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }
}

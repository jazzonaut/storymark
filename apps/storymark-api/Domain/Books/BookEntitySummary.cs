namespace Storymark.Api.Domain.Books;

/// <summary>A spoiler-safe entity summary cached at a reading position (docs/plan.md
/// §12.10, §16.3). Generated lazily on first tap and reused for every later tap at the
/// same position. <see cref="UpToSection"/>/<see cref="UpToLocation"/> bound what the
/// summary may mention — only content the reader has reached — so a tap early in the
/// book yields a thinner picture than the same entity tapped later.</summary>
internal sealed class BookEntitySummary
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    public Guid EntityId { get; set; }

    public int UpToSection { get; set; }

    public int UpToLocation { get; set; }

    public required string ShortSummary { get; set; }

    public required IReadOnlyList<string> KnownFacts { get; set; }

    public required IReadOnlyList<string> Relationships { get; set; }

    public required IReadOnlyList<string> OpenQuestions { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }
}

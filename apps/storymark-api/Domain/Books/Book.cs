namespace Storymark.Api.Domain.Books;

/// <summary>An imported book. The original file and extracted artifacts live on
/// disk keyed by <see cref="ContentHash"/> (docs/plan.md §14); Postgres is the
/// queryable source of truth (docs/plan.md §12.1).</summary>
internal sealed class Book
{
    public Guid Id { get; set; }

    /// <summary>SHA-256 of the original file; the dedup key and the on-disk
    /// directory name under <c>/data/books/</c>.</summary>
    public required string ContentHash { get; set; }

    public required string Title { get; set; }

    public string? Author { get; set; }

    public string? Language { get; set; }

    public required string Format { get; set; }

    public required string FilePath { get; set; }

    public string? CoverPath { get; set; }

    public ProcessingStatus ProcessingStatus { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

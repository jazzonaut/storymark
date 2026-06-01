namespace Storymark.Api.Services.Books;

/// <summary>The rolling "story so far" at-or-before a reading position
/// (<c>GET /api/books/{id}/memory</c>) — powers the section-entry recap card.</summary>
internal sealed record MemoryView
{
    public required int UpToSection { get; init; }

    public required int UpToLocation { get; init; }

    public required string SummarySoFar { get; init; }

    public required IReadOnlyList<string> Timeline { get; init; }

    public required IReadOnlyList<string> OpenThreads { get; init; }
}

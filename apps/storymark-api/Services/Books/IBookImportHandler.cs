namespace Storymark.Api.Services.Books;

/// <summary>Metadata read from a book file without full content extraction.</summary>
internal sealed record BookMetadata
{
    public required string Title { get; init; }

    public string? Author { get; init; }

    public string? Language { get; init; }
}

/// <summary>Format-specific import behaviour. <see cref="SniffMetadataAsync"/> is
/// the cheap upload-time read (S1.1); <see cref="ParseAsync"/> is the full
/// content extraction the background job runs (S2.1, docs/plan.md §15.2).</summary>
internal interface IBookImportHandler
{
    bool CanHandle(string format);

    Task<BookMetadata> SniffMetadataAsync(string filePath, CancellationToken ct);

    Task<BookContent> ParseAsync(string filePath, CancellationToken ct);
}

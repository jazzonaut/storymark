namespace Storymark.Api.Services.Books;

/// <summary>Metadata read from a book file without full content extraction.</summary>
internal sealed record BookMetadata
{
    public required string Title { get; init; }

    public string? Author { get; init; }

    public string? Language { get; init; }
}

/// <summary>Format-specific import behaviour. S1.1 only sniffs metadata; the
/// same handler grows full parsing (sections, cover, sanitised HTML) in S2.1
/// (docs/issues.md S2.1).</summary>
internal interface IBookImportHandler
{
    bool CanHandle(string format);

    Task<BookMetadata> SniffMetadataAsync(string filePath, CancellationToken ct);
}

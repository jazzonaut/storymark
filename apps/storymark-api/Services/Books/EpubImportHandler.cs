using VersOne.Epub;

namespace Storymark.Api.Services.Books;

/// <summary>Reads title/author/language from an EPUB's package metadata via
/// VersOne.Epub. Full content extraction (sections, cover, sanitised HTML) is
/// deferred to S2.1 — this handler only sniffs metadata at upload time.</summary>
internal sealed class EpubImportHandler : IBookImportHandler
{
    public bool CanHandle(string format) =>
        string.Equals(format, "epub", StringComparison.OrdinalIgnoreCase);

    public async Task<BookMetadata> SniffMetadataAsync(string filePath, CancellationToken ct)
    {
        var epub = await EpubReader.ReadBookAsync(filePath);

        var author = string.IsNullOrWhiteSpace(epub.Author) ? null : epub.Author;
        var language = epub.Schema.Package.Metadata.Languages
            .Select(l => l.Language)
            .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));

        return new BookMetadata
        {
            Title = epub.Title,
            Author = author,
            Language = language,
        };
    }
}

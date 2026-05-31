using Storymark.Api.Domain.Books;
using VersOne.Epub;

namespace Storymark.Api.Services.Books;

/// <summary>Reads EPUBs via VersOne.Epub. <see cref="SniffMetadataAsync"/> sniffs
/// title/author/language at upload time (S1.1); <see cref="ParseAsync"/> reads the
/// cover, reading order, per-section XHTML, and embedded images for the background
/// processing job (S2.1).</summary>
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

    public async Task<BookContent> ParseAsync(string filePath, CancellationToken ct)
    {
        var epub = await EpubReader.ReadBookAsync(filePath);
        var titles = BuildTitleMap(epub.Navigation);

        var sections = epub.ReadingOrder
            .Select(file => new ParsedSection
            {
                SourceHref = EpubPath.Normalize(file.FilePath),
                Title = titles.GetValueOrDefault(EpubPath.Normalize(file.FilePath)),
                RawXhtml = file.Content,
            })
            .ToList();

        var images = new Dictionary<string, ParsedImage>(StringComparer.Ordinal);
        foreach (var image in epub.Content.Images.Local)
        {
            var path = EpubPath.Normalize(image.FilePath);
            images[path] = new ParsedImage { Path = path, Content = image.Content };
        }

        return new BookContent
        {
            Cover = ExtractCover(epub),
            Sections = sections,
            ImagesByPath = images,
        };
    }

    private static ParsedCover? ExtractCover(EpubBook epub)
    {
        var cover = epub.Content.Cover;
        if (cover is not null)
        {
            return new ParsedCover
            {
                Bytes = cover.Content,
                Extension = ImageContentTypes.ExtensionForMime(cover.ContentMimeType) ?? ".jpg",
            };
        }

        // EPUB 2 books sometimes expose only the raw cover bytes with no manifest
        // entry; default the extension since the MIME type is unknown here.
        return epub.CoverImage is { Length: > 0 } bytes
            ? new ParsedCover { Bytes = bytes, Extension = ".jpg" }
            : null;
    }

    private static Dictionary<string, string> BuildTitleMap(List<EpubNavigationItem>? navigation)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (navigation is not null)
        {
            CollectTitles(navigation, map);
        }

        return map;
    }

    private static void CollectTitles(List<EpubNavigationItem> items, Dictionary<string, string> map)
    {
        foreach (var item in items)
        {
            var file = item.HtmlContentFile;
            if (file is not null && !string.IsNullOrWhiteSpace(item.Title))
            {
                map.TryAdd(EpubPath.Normalize(file.FilePath), item.Title.Trim());
            }

            if (item.NestedItems.Count > 0)
            {
                CollectTitles(item.NestedItems, map);
            }
        }
    }
}

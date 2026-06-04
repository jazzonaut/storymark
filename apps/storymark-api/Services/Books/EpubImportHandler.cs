using System.IO.Compression;
using Microsoft.Extensions.Options;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Storage;
using VersOne.Epub;
using VersOne.Epub.Options;

namespace Storymark.Api.Services.Books;

/// <summary>Reads EPUBs via VersOne.Epub. <see cref="SniffMetadataAsync"/> sniffs
/// title/author/language at upload time (S1.1); <see cref="ParseAsync"/> reads the
/// cover, reading order, per-section XHTML, and embedded images for the background
/// processing job (S2.1). Both reject an over-large archive before reading it into
/// memory (<see cref="EnsureWithinLimits"/>), so a zip bomb can't OOM the worker.</summary>
internal sealed class EpubImportHandler(IOptions<StorageOptions> options) : IBookImportHandler
{
    // Commercial EPUBs routinely ship publisher defects (e.g. an NCX entry pointing
    // at a file absent from the manifest); the default STRICT preset rejects the
    // whole book over them, and the uploader can't fix the file. RELAXED skips the
    // broken pieces but still fails on unreadable archives (ADR-0026).
    private static readonly EpubReaderOptions ReaderOptions = new(EpubReaderOptionsPreset.RELAXED);

    private readonly long _maxDecompressedBytes = options.Value.MaxDecompressedBytes;
    private readonly int _maxEntries = options.Value.MaxArchiveEntries;

    public bool CanHandle(string format) =>
        string.Equals(format, "epub", StringComparison.OrdinalIgnoreCase);

    public async Task<BookMetadata> SniffMetadataAsync(string filePath, CancellationToken ct)
    {
        EnsureWithinLimits(filePath);
        var epub = await ReadBookAsync(filePath);

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
        EnsureWithinLimits(filePath);
        var epub = await ReadBookAsync(filePath);
        var (wholeFileTitles, fragmentTitles) = BuildTitleMaps(epub.Navigation);

        var sections = epub.ReadingOrder
            .Select(file =>
            {
                var path = EpubPath.Normalize(file.FilePath);
                return new ParsedSection
                {
                    SourceHref = path,
                    Title = wholeFileTitles.GetValueOrDefault(path),
                    FragmentTitle = fragmentTitles.GetValueOrDefault(path),
                    RawXhtml = file.Content,
                };
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

    // Unlike the parameterless overload, the options overload is annotated as
    // returning EpubBook? even though it never returns null (it either builds the
    // book or throws), so narrow here once for both call sites.
    private static async Task<EpubBook> ReadBookAsync(string filePath)
    {
        var epub = await EpubReader.ReadBookAsync(filePath, ReaderOptions);
        return epub ?? throw new InvalidDataException($"EPUB reader returned no book for \"{filePath}\".");
    }

    /// <summary>Rejects an archive whose declared uncompressed size or entry count
    /// exceeds the configured limits, reading only the zip central directory (no
    /// decompression) so the check happens before the book is loaded into memory.</summary>
    private void EnsureWithinLimits(string filePath)
    {
        using var archive = ZipFile.OpenRead(filePath);
        long totalUncompressed = 0;
        var entryCount = 0;
        foreach (var entry in archive.Entries)
        {
            entryCount++;
            if (entryCount > _maxEntries)
            {
                throw new InvalidDataException(
                    $"EPUB has more than {_maxEntries} archive entries; refusing to process.");
            }

            totalUncompressed += entry.Length;
            if (totalUncompressed > _maxDecompressedBytes)
            {
                throw new InvalidDataException(
                    $"EPUB decompresses to more than {_maxDecompressedBytes} bytes; refusing to process.");
            }
        }
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

    private static (Dictionary<string, string> WholeFile, Dictionary<string, string> Fragment) BuildTitleMaps(
        List<EpubNavigationItem>? navigation)
    {
        var wholeFile = new Dictionary<string, string>(StringComparer.Ordinal);
        var fragment = new Dictionary<string, string>(StringComparer.Ordinal);
        if (navigation is not null)
        {
            CollectTitles(navigation, wholeFile, fragment);
        }

        return (wholeFile, fragment);
    }

    private static void CollectTitles(
        List<EpubNavigationItem> items,
        Dictionary<string, string> wholeFile,
        Dictionary<string, string> fragment)
    {
        foreach (var item in items)
        {
            var file = item.HtmlContentFile;
            if (file is not null && !string.IsNullOrWhiteSpace(item.Title))
            {
                // A nav entry whose link carries an anchor names a position *inside* a
                // file, not the file itself. Calibre routinely emits a part/section anchor
                // at the very end of the preceding chapter's split file, so titling the
                // whole file by that anchor mislabels the chapter (e.g. "CHAPTER TWENTY-SIX"
                // shown as "PART 4 : PERSUASION"). Keep anchored titles only as a
                // lower-priority fallback than the file's own leading heading.
                var map = string.IsNullOrWhiteSpace(item.Link?.Anchor) ? wholeFile : fragment;
                map.TryAdd(EpubPath.Normalize(file.FilePath), item.Title.Trim());
            }

            if (item.NestedItems.Count > 0)
            {
                CollectTitles(item.NestedItems, wholeFile, fragment);
            }
        }
    }
}

namespace Storymark.Api.Services.Books;

/// <summary>A cover image extracted from a book file: the raw bytes plus the
/// file extension (with leading dot) to store it under.</summary>
internal sealed record ParsedCover
{
    public required byte[] Bytes { get; init; }

    public required string Extension { get; init; }
}

/// <summary>One reading-order unit: its EPUB-internal path, the raw un-sanitised XHTML,
/// and up to two navigation titles. <see cref="Title"/> comes from a whole-file nav entry
/// (the TOC naming this file outright) and is authoritative. <see cref="FragmentTitle"/>
/// comes from a nav entry whose link carries an anchor — a position inside the file that
/// Calibre often places at a split boundary — so it ranks below the file's own heading.</summary>
internal sealed record ParsedSection
{
    public required string SourceHref { get; init; }

    public string? Title { get; init; }

    public string? FragmentTitle { get; init; }

    public required string RawXhtml { get; init; }
}

/// <summary>An embedded image resource, keyed in <see cref="BookContent.ImagesByPath"/>
/// by its normalised archive path.</summary>
internal sealed record ParsedImage
{
    public required string Path { get; init; }

    public required byte[] Content { get; init; }
}

/// <summary>The fully-read content of a book file, ready for sanitisation and
/// persistence (docs/plan.md §15.2 steps 3–6). Distinct from
/// <see cref="BookMetadata"/>, which is the cheap upload-time sniff.</summary>
internal sealed record BookContent
{
    public ParsedCover? Cover { get; init; }

    public required IReadOnlyList<ParsedSection> Sections { get; init; }

    public required IReadOnlyDictionary<string, ParsedImage> ImagesByPath { get; init; }
}

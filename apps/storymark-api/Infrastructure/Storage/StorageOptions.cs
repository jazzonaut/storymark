namespace Storymark.Api.Infrastructure.Storage;

/// <summary>Where book artifacts live on disk (docs/plan.md §14). Bound from the
/// <c>Storage</c> configuration section; the compose stack mounts a volume at
/// <c>/data</c> and sets <c>Storage__RootPath</c>.</summary>
internal sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string RootPath { get; init; } = "/data";

    /// <summary>Upper bound on an EPUB's total uncompressed size, checked from the
    /// zip central directory before the archive is read into memory — a zip-bomb
    /// guard so a small upload can't inflate to gigabytes and OOM the worker.
    /// Default 1&#160;GiB; legitimate image-heavy books stay well under it.</summary>
    public long MaxDecompressedBytes { get; init; } = 1L * 1024 * 1024 * 1024;

    /// <summary>Upper bound on the number of entries in an EPUB archive (a second
    /// zip-bomb dimension: many tiny entries). Default 10,000.</summary>
    public int MaxArchiveEntries { get; init; } = 10_000;
}

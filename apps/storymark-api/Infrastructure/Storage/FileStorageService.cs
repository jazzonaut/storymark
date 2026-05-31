using System.Buffers;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Storymark.Api.Infrastructure.Storage;

internal enum BookStoreOutcome
{
    Stored,
    TooLarge,
    NotEpub,
    Empty,
}

internal sealed record BookStoreResult
{
    public required BookStoreOutcome Outcome { get; init; }

    public string? ContentHash { get; init; }

    public string? FilePath { get; init; }

    public bool AlreadyExisted { get; init; }

    public static BookStoreResult Stored(string hash, string path, bool alreadyExisted) =>
        new()
        {
            Outcome = BookStoreOutcome.Stored,
            ContentHash = hash,
            FilePath = path,
            AlreadyExisted = alreadyExisted,
        };

    public static BookStoreResult Failure(BookStoreOutcome outcome) => new() { Outcome = outcome };
}

/// <summary>Owns the on-disk book layout under <c>{root}/books/{hash}/</c>
/// (docs/plan.md §14). The original upload is streamed straight to a temp file
/// while its SHA-256 is computed and the size cap enforced, so a 200&#160;MB file
/// never sits in memory; it is then moved into place keyed by content hash, which
/// also gives us free dedup.</summary>
internal sealed class FileStorageService(IOptions<StorageOptions> options)
{
    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];

    private readonly string _root = options.Value.RootPath;

    public async Task<BookStoreResult> StoreBookOriginalAsync(Stream source, long maxBytes, CancellationToken ct)
    {
        var tmpDir = Path.Combine(_root, "tmp");
        Directory.CreateDirectory(tmpDir);
        var tmpPath = Path.Combine(tmpDir, Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            var (outcome, hash) = await WriteAndHashAsync(source, tmpPath, maxBytes, ct);
            if (outcome != BookStoreOutcome.Stored || hash is null)
            {
                return BookStoreResult.Failure(outcome);
            }

            var bookDir = Path.Combine(_root, "books", hash);
            var finalPath = Path.Combine(bookDir, "original.epub");

            if (File.Exists(finalPath))
            {
                return BookStoreResult.Stored(hash, finalPath, alreadyExisted: true);
            }

            Directory.CreateDirectory(bookDir);
            File.Move(tmpPath, finalPath);
            return BookStoreResult.Stored(hash, finalPath, alreadyExisted: false);
        }
        finally
        {
            if (File.Exists(tmpPath))
            {
                File.Delete(tmpPath);
            }
        }
    }

    /// <summary>Streams <paramref name="source"/> to <paramref name="tmpPath"/>,
    /// validating the ZIP signature on the first chunk and the size cap as it
    /// goes. On success returns <see cref="BookStoreOutcome.Stored"/> with the
    /// lowercase hex SHA-256.</summary>
    private static async Task<(BookStoreOutcome Outcome, string? Hash)> WriteAndHashAsync(
        Stream source, string tmpPath, long maxBytes, CancellationToken ct)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long total = 0;
        var header = new byte[ZipMagic.Length];
        var headerFilled = 0;
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            await using var tmp = new FileStream(
                tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                // ReadAsync may return fewer than 4 bytes (or split the signature
                // across chunks), so accumulate the header before validating it.
                if (headerFilled < header.Length)
                {
                    var copy = Math.Min(header.Length - headerFilled, read);
                    Array.Copy(buffer, 0, header, headerFilled, copy);
                    headerFilled += copy;
                    if (headerFilled == header.Length && !header.AsSpan().SequenceEqual(ZipMagic))
                    {
                        return (BookStoreOutcome.NotEpub, null);
                    }
                }

                total += read;
                if (total > maxBytes)
                {
                    return (BookStoreOutcome.TooLarge, null);
                }

                hasher.AppendData(buffer, 0, read);
                await tmp.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (total == 0)
        {
            return (BookStoreOutcome.Empty, null);
        }

        // Smaller than the ZIP signature → cannot be an EPUB.
        if (headerFilled < header.Length)
        {
            return (BookStoreOutcome.NotEpub, null);
        }

        return (BookStoreOutcome.Stored, Convert.ToHexStringLower(hasher.GetHashAndReset()));
    }
}

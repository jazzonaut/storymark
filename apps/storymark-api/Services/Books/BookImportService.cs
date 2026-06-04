using Hangfire;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Storage;
using Storymark.Api.Jobs;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Services.Books;

internal enum BookImportError
{
    None,
    Empty,
    NotEpub,
    TooLarge,
    DecompressedTooLarge,
}

internal sealed record BookImportOutcome
{
    public Book? Book { get; init; }

    public BookImportError Error { get; init; }

    public long MaxBytes { get; init; }

    public static BookImportOutcome Ok(Book book) => new() { Book = book };

    public static BookImportOutcome Failed(BookImportError error, long maxBytes) =>
        new() { Error = error, MaxBytes = maxBytes };
}

/// <summary>Upload-time book import (docs/plan.md §15.1): store the original by
/// hash, dedup against existing rows, sniff metadata, and persist a <c>books</c>
/// row in <see cref="ProcessingStatus.Uploaded"/>. The background processing
/// pipeline that advances the status arrives in S2.1.</summary>
internal sealed class BookImportService(
    StorymarkDbContext db,
    FileStorageService storage,
    IEnumerable<IBookImportHandler> handlers,
    ISettingsService settings,
    IBackgroundJobClient jobs)
{
    private const string Format = "epub";

    public async Task<BookImportOutcome> ImportAsync(
        string fileName, Stream content, Guid userId, CancellationToken ct)
    {
        var maxBytes = settings.GetInt("upload.maxBytes");
        var stored = await storage.StoreBookOriginalAsync(content, maxBytes, ct);

        if (stored.Outcome == BookStoreOutcome.TooLarge)
        {
            return BookImportOutcome.Failed(BookImportError.TooLarge, maxBytes);
        }

        if (stored.Outcome == BookStoreOutcome.NotEpub)
        {
            return BookImportOutcome.Failed(BookImportError.NotEpub, maxBytes);
        }

        if (stored.Outcome == BookStoreOutcome.Empty || stored.ContentHash is null || stored.FilePath is null)
        {
            return BookImportOutcome.Failed(BookImportError.Empty, maxBytes);
        }

        var contentHash = stored.ContentHash;
        var filePath = stored.FilePath;

        // Dedup is scoped to the uploader's own rows: a second copy of content
        // someone else holds — public included — becomes its own row sharing the
        // on-disk directory, never a reuse that would entangle ownership (the
        // uploader must own what their upload returns; ADR-0023).
        var existing = await db.Books
            .FirstOrDefaultAsync(b => b.ContentHash == contentHash && b.OwnerUserId == userId, ct);
        if (existing is not null)
        {
            return BookImportOutcome.Ok(existing);
        }

        return await CreateBookAsync(fileName, contentHash, filePath, userId, maxBytes, ct);
    }

    private async Task<BookImportOutcome> CreateBookAsync(
        string fileName, string contentHash, string filePath, Guid userId, long maxBytes, CancellationToken ct)
    {
        var metadata = await SniffMetadataAsync(filePath, ct);
        if (metadata is null)
        {
            return BookImportOutcome.Failed(BookImportError.DecompressedTooLarge, maxBytes);
        }

        var title = string.IsNullOrWhiteSpace(metadata.Title)
            ? Path.GetFileNameWithoutExtension(fileName)
            : metadata.Title;

        var now = DateTimeOffset.UtcNow;
        var book = new Book
        {
            Id = Guid.NewGuid(),
            ContentHash = contentHash,
            OwnerUserId = userId,
            Title = title,
            Author = metadata.Author,
            Language = metadata.Language,
            Format = Format,
            FilePath = filePath,
            ProcessingStatus = ProcessingStatus.Uploaded,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Books.Add(book);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // The same user's concurrent upload of identical content won the race
            // on the unique books_content_hash_owner index between the dedup check
            // above and this insert. Return its row (the same outcome the dedup
            // branch would) instead of surfacing the constraint violation as a 500.
            // It already enqueued the job.
            db.Entry(book).State = EntityState.Detached;
            var winner = await db.Books.FirstOrDefaultAsync(
                b => b.ContentHash == contentHash && b.OwnerUserId == userId, ct);
            if (winner is not null)
            {
                return BookImportOutcome.Ok(winner);
            }

            throw;
        }

        // Only newly-created books are enqueued; the dedup path above returns the
        // existing row (already processed or in flight) without re-queuing.
        jobs.Enqueue<ProcessBookJob>(j => j.RunAsync(book.Id, CancellationToken.None));
        return BookImportOutcome.Ok(book);
    }

    /// <summary>Null when the archive's declared decompressed size/entry count
    /// exceeds the safety limits (zip-bomb guard). The original is already stored
    /// under its hash; it stays a bounded, recoverable orphan and the upload is
    /// rejected cleanly.</summary>
    private async Task<BookMetadata?> SniffMetadataAsync(string filePath, CancellationToken ct)
    {
        var handler = handlers.First(h => h.CanHandle(Format));
        try
        {
            return await handler.SniffMetadataAsync(filePath, ct);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

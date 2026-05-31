using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Storage;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Services.Books;

internal enum BookImportError
{
    None,
    Empty,
    NotEpub,
    TooLarge,
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
    ISettingsService settings)
{
    private const string Format = "epub";

    public async Task<BookImportOutcome> ImportAsync(string fileName, Stream content, CancellationToken ct)
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

        var existing = await db.Books.FirstOrDefaultAsync(b => b.ContentHash == contentHash, ct);
        if (existing is not null)
        {
            return BookImportOutcome.Ok(existing);
        }

        var handler = handlers.First(h => h.CanHandle(Format));
        var metadata = await handler.SniffMetadataAsync(filePath, ct);

        var title = string.IsNullOrWhiteSpace(metadata.Title)
            ? Path.GetFileNameWithoutExtension(fileName)
            : metadata.Title;

        var now = DateTimeOffset.UtcNow;
        var book = new Book
        {
            Id = Guid.NewGuid(),
            ContentHash = contentHash,
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
        await db.SaveChangesAsync(ct);
        return BookImportOutcome.Ok(book);
    }
}

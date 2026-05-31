using Hangfire;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Storage;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Jobs;

/// <summary>Background processing for an uploaded book (docs/plan.md §15.2):
/// parse the EPUB, store the cover and per-section artifacts, and persist
/// <see cref="BookSection"/>/<see cref="BookParagraph"/> rows, driving
/// <see cref="ProcessingStatus"/> from <c>Uploaded</c> through <c>Parsing</c> to
/// <c>Parsed</c>. Chunking, embedding, and extraction are later slices.
///
/// <para><see cref="AutomaticRetryAttribute"/> is set to zero: a parse failure is
/// deterministic (a malformed file), so a retry would just fail again — it surfaces
/// as Failed in the dashboard instead. Re-running (e.g. a manual requeue) is safe:
/// existing sections are cleared first.</para></summary>
internal sealed class ProcessBookJob(
    StorymarkDbContext db,
    FileStorageService storage,
    IEnumerable<IBookImportHandler> handlers,
    ContentSanitizationService sanitizer,
    ILogger<ProcessBookJob> logger)
{
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(Guid bookId, CancellationToken ct)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == bookId, ct);
        if (book is null)
        {
            logger.LogWarning("ProcessBookJob skipped: book {BookId} not found", bookId);
            return;
        }

        try
        {
            await ClearExistingSectionsAsync(bookId, ct);
            await SetStatusAsync(book, ProcessingStatus.Parsing, ct);

            var handler = handlers.First(h => h.CanHandle(book.Format));
            var content = await handler.ParseAsync(book.FilePath, ct);

            if (content.Cover is { } cover)
            {
                book.CoverPath = await storage.StoreCoverAsync(book.ContentHash, cover.Bytes, cover.Extension, ct);
            }

            await PersistSectionsAsync(book, content, ct);
            await SetStatusAsync(book, ProcessingStatus.Parsed, ct);
            logger.LogInformation(
                "Parsed book {BookId}: {SectionCount} sections", bookId, content.Sections.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ProcessBookJob failed for book {BookId}", bookId);
            await SetStatusAsync(book, ProcessingStatus.Failed, ct);
            throw;
        }
    }

    private async Task PersistSectionsAsync(Book book, BookContent content, CancellationToken ct)
    {
        var storedResources = new HashSet<string>(StringComparer.Ordinal);
        var now = DateTimeOffset.UtcNow;
        var sectionIndex = 0;

        foreach (var parsed in content.Sections)
        {
            sectionIndex++;
            var result = sanitizer.Sanitize(parsed.RawXhtml, parsed.SourceHref, content.ImagesByPath, book.Id);
            await StoreResourcesAsync(book.ContentHash, result.UsedImagePaths, content.ImagesByPath, storedResources, ct);

            var (htmlPath, textPath) = await storage.StoreSectionAsync(
                book.ContentHash, sectionIndex, result.SanitizedHtml, result.PlainText, ct);

            var section = new BookSection
            {
                Id = Guid.NewGuid(),
                BookId = book.Id,
                SectionIndex = sectionIndex,
                Title = parsed.Title,
                SourceHref = parsed.SourceHref,
                HtmlPath = htmlPath,
                TextPath = textPath,
                TextHash = TextHash(result.PlainText),
                WordCount = result.WordCount,
                CreatedAt = now,
            };
            db.BookSections.Add(section);
            AddParagraphs(book.Id, section, result.Paragraphs, now);
        }

        await db.SaveChangesAsync(ct);
    }

    private void AddParagraphs(
        Guid bookId, BookSection section, IReadOnlyList<SanitizedParagraph> paragraphs, DateTimeOffset now)
    {
        for (var i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            db.BookParagraphs.Add(new BookParagraph
            {
                Id = Guid.NewGuid(),
                BookId = bookId,
                SectionId = section.Id,
                SectionIndex = section.SectionIndex,
                ParagraphIndex = i,
                LocationStart = paragraph.LocationStart,
                LocationEnd = paragraph.LocationEnd,
                Html = paragraph.Html,
                PlainText = paragraph.PlainText,
                CreatedAt = now,
            });
        }
    }

    private async Task StoreResourcesAsync(
        string hash,
        IReadOnlySet<string> usedPaths,
        IReadOnlyDictionary<string, ParsedImage> images,
        HashSet<string> alreadyStored,
        CancellationToken ct)
    {
        foreach (var path in usedPaths)
        {
            if (alreadyStored.Add(path) && images.TryGetValue(path, out var image))
            {
                await storage.StoreResourceAsync(hash, path, image.Content, ct);
            }
        }
    }

    private async Task ClearExistingSectionsAsync(Guid bookId, CancellationToken ct)
    {
        // Cascades to book_paragraphs; keeps a re-run idempotent.
        await db.BookSections.Where(s => s.BookId == bookId).ExecuteDeleteAsync(ct);
    }

    private async Task SetStatusAsync(Book book, ProcessingStatus status, CancellationToken ct)
    {
        book.ProcessingStatus = status;
        book.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static string TextHash(string text)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
        return Convert.ToHexStringLower(bytes);
    }
}

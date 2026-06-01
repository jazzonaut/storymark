using Hangfire;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Storage;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Jobs;

/// <summary>Background processing for an uploaded book (docs/plan.md §15.2):
/// parse the EPUB, store the cover and per-section artifacts, persist
/// <see cref="BookSection"/>/<see cref="BookParagraph"/> rows, then chunk every
/// section into <see cref="BookChunk"/> rows. Drives <see cref="ProcessingStatus"/>
/// <c>Uploaded → Parsing → Parsed → Chunking → ExtractingPerSection</c>, then enqueues
/// <see cref="SectionExtractionJob"/>, which extracts per-section structure and chains
/// to <see cref="EmbeddingJob"/> — embedding the chunks and finishing at <c>Indexed</c>.
///
/// <para><see cref="AutomaticRetryAttribute"/> is set to zero: a parse failure is
/// deterministic (a malformed file), so a retry would just fail again — it surfaces
/// as Failed in the dashboard instead. Re-running (e.g. a manual requeue) is safe:
/// existing sections are cleared first, which cascade-deletes their paragraphs and
/// chunks.</para></summary>
internal sealed class ProcessBookJob(
    StorymarkDbContext db,
    FileStorageService storage,
    IEnumerable<IBookImportHandler> handlers,
    ContentSanitizationService sanitizer,
    IBackgroundJobClient jobs,
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
            book.ProcessingError = null;
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

            await ChunkBookAsync(book, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ProcessBookJob failed for book {BookId}", bookId);
            book.ProcessingError = ex.Message;
            await SetStatusAsync(book, ProcessingStatus.Failed, ct);
            throw;
        }
    }

    private async Task ChunkBookAsync(Book book, CancellationToken ct)
    {
        await SetStatusAsync(book, ProcessingStatus.Chunking, ct);

        var sections = await db.BookSections
            .Where(s => s.BookId == book.Id)
            .OrderBy(s => s.SectionIndex)
            .ToListAsync(ct);

        var paragraphsBySection = (await db.BookParagraphs
                .Where(p => p.BookId == book.Id)
                .OrderBy(p => p.SectionIndex)
                .ThenBy(p => p.ParagraphIndex)
                .ToListAsync(ct))
            .GroupBy(p => p.SectionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<BookParagraph>)[.. g]);

        var now = DateTimeOffset.UtcNow;
        var chunkCount = 0;

        foreach (var section in sections)
        {
            if (!paragraphsBySection.TryGetValue(section.Id, out var paragraphs))
            {
                continue;
            }

            var drafts = SectionChunkingService.Chunk(paragraphs);
            for (var chunkIndex = 0; chunkIndex < drafts.Count; chunkIndex++)
            {
                var draft = drafts[chunkIndex];
                db.BookChunks.Add(new BookChunk
                {
                    Id = Guid.NewGuid(),
                    BookId = book.Id,
                    SectionId = section.Id,
                    SectionIndex = section.SectionIndex,
                    ChunkIndex = chunkIndex,
                    LocationStart = draft.LocationStart,
                    LocationEnd = draft.LocationEnd,
                    Text = draft.Text,
                    TokenEstimate = draft.TokenEstimate,
                    CreatedAt = now,
                });
                chunkCount++;
            }
        }

        await db.SaveChangesAsync(ct);

        await SetStatusAsync(book, ProcessingStatus.ExtractingPerSection, ct);
        logger.LogInformation("Chunked book {BookId}: {ChunkCount} chunks", book.Id, chunkCount);

        // SectionExtractionJob runs first (it needs only section text, not vectors),
        // then chains to EmbeddingJob → Indexed. Keeping EmbeddingJob as the terminal
        // step means an S3.4 reindex (which only re-enqueues EmbeddingJob) never
        // re-extracts. Both run in the background so chunking stays fast.
        jobs.Enqueue<SectionExtractionJob>(j => j.RunAsync(book.Id, CancellationToken.None));
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
                Title = parsed.Title ?? result.HeadingTitle ?? parsed.FragmentTitle,
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

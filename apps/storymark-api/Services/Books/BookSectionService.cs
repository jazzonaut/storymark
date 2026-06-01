using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Storage;

namespace Storymark.Api.Services.Books;

/// <summary>Read access to parsed section content for the reader endpoints
/// (docs/plan.md §11.3). DB queries and the on-disk HTML/resource reads live here,
/// not in the endpoints (layering).</summary>
internal sealed class BookSectionService(
    StorymarkDbContext db, FileStorageService storage, BookEntityService entities)
{
    /// <summary>The section manifest, or <c>null</c> if the book does not exist.</summary>
    public async Task<IReadOnlyList<SectionSummaryView>?> GetManifestAsync(Guid bookId, CancellationToken ct)
    {
        if (!await db.Books.AnyAsync(b => b.Id == bookId, ct))
        {
            return null;
        }

        return await db.BookSections.AsNoTracking()
            .Where(s => s.BookId == bookId)
            .OrderBy(s => s.SectionIndex)
            .Select(s => new SectionSummaryView
            {
                SectionIndex = s.SectionIndex,
                Title = s.Title,
                WordCount = s.WordCount,
                ParagraphCount = db.BookParagraphs.Count(p => p.SectionId == s.Id),
            })
            .ToListAsync(ct);
    }

    /// <summary>Full content for one section, or <c>null</c> if the book/section
    /// does not exist.</summary>
    public async Task<SectionContentView?> GetSectionAsync(Guid bookId, int sectionIndex, CancellationToken ct)
    {
        var section = await db.BookSections.AsNoTracking()
            .FirstOrDefaultAsync(s => s.BookId == bookId && s.SectionIndex == sectionIndex, ct);
        if (section is null)
        {
            return null;
        }

        var html = section.HtmlPath is null
            ? string.Empty
            : await storage.ReadTextAsync(section.HtmlPath, ct) ?? string.Empty;

        var paragraphs = await db.BookParagraphs.AsNoTracking()
            .Where(p => p.SectionId == section.Id)
            .OrderBy(p => p.ParagraphIndex)
            .Select(p => new ParagraphView
            {
                ParagraphIndex = p.ParagraphIndex,
                LocationStart = p.LocationStart,
                LocationEnd = p.LocationEnd,
                Html = p.Html,
                Text = p.PlainText,
            })
            .ToListAsync(ct);

        var visibleEntities = await entities.GetVisibleForSectionAsync(bookId, sectionIndex, ct);

        return new SectionContentView
        {
            SectionIndex = section.SectionIndex,
            Title = section.Title,
            Html = html,
            Paragraphs = paragraphs,
            Entities = visibleEntities,
        };
    }

    /// <summary>Resolves an inline resource request to a servable image file, or
    /// <c>null</c> if the book is unknown, the path escapes/doesn't exist, or the
    /// file is not an image type we serve.</summary>
    public async Task<BookResourceView?> ResolveResourceAsync(Guid bookId, string requestPath, CancellationToken ct)
    {
        var hash = await db.Books.Where(b => b.Id == bookId)
            .Select(b => b.ContentHash)
            .FirstOrDefaultAsync(ct);
        if (hash is null)
        {
            return null;
        }

        var filePath = storage.ResolveResourcePath(hash, requestPath);
        if (filePath is null)
        {
            return null;
        }

        var contentType = ImageContentTypes.ContentTypeForExtension(Path.GetExtension(filePath));
        return contentType is null
            ? null
            : new BookResourceView { FilePath = filePath, ContentType = contentType };
    }
}

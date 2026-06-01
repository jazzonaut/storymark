using Microsoft.EntityFrameworkCore;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Storage;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Jobs;

/// <summary>One-off backfill for sections imported before titles fell back to the
/// section's first heading (the TOC names only a subset of spine files, so the rest
/// were left untitled). For every section with no title, it re-reads the already-stored
/// sanitised HTML and lifts the first heading — no EPUB re-parse, no LLM or embedding
/// work, since <see cref="Domain.Books.BookSection.Title"/> is display-only. Idempotent:
/// it only touches null titles, so re-running never overwrites a TOC title and a section
/// with no heading stays null (the reader keeps its "Untitled section" fallback).</summary>
internal sealed class BackfillSectionTitlesJob(
    StorymarkDbContext db,
    FileStorageService storage,
    ContentSanitizationService sanitizer,
    ILogger<BackfillSectionTitlesJob> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var sections = await db.BookSections
            .Where(s => s.Title == null && s.HtmlPath != null)
            .OrderBy(s => s.BookId)
            .ThenBy(s => s.SectionIndex)
            .ToListAsync(ct);

        var filled = 0;
        foreach (var section in sections)
        {
            if (section.HtmlPath is not { } htmlPath)
            {
                continue;
            }

            var html = await storage.ReadTextAsync(htmlPath, ct);
            if (html is null)
            {
                continue;
            }

            var title = sanitizer.ExtractHeadingTitle(html);
            if (title is null)
            {
                continue;
            }

            section.Title = title;
            filled++;
        }

        if (filled > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation(
            "Backfilled section titles: {Filled} of {Scanned} untitled sections now have a heading title",
            filled, sections.Count);
    }
}

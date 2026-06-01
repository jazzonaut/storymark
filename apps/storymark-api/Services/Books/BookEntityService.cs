using Microsoft.EntityFrameworkCore;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Services.Books;

/// <summary>Read access to the Story Guide entities (docs/plan.md §5.3, §16). Entities
/// are surfaced only once the reader has reached where they are introduced — the same
/// <c>first_seen ≤ position</c> boundary the selection-hint path uses — so a not-yet-met
/// character never appears in the highlight set or the guide list.</summary>
internal sealed class BookEntityService(StorymarkDbContext db)
{
    /// <summary>Entities introduced in or before <paramref name="sectionIndex"/>, for
    /// in-text highlighting. Caller already knows the book exists (the section loaded).</summary>
    public async Task<IReadOnlyList<SectionEntityView>> GetVisibleForSectionAsync(
        Guid bookId, int sectionIndex, CancellationToken ct) =>
        await db.BookEntities.AsNoTracking()
            .Where(e => e.BookId == bookId
                && e.FirstSeenSection != null
                && e.FirstSeenSection <= sectionIndex)
            .OrderBy(e => e.FirstSeenSection)
            .ThenBy(e => e.CanonicalName)
            .Select(e => new SectionEntityView
            {
                EntityId = e.Id,
                CanonicalName = e.CanonicalName,
                EntityType = e.EntityType.ToString(),
                Aliases = e.Aliases,
                FirstSeenSection = e.FirstSeenSection,
            })
            .ToListAsync(ct);

    /// <summary>The Story Guide list: entities visible at <c>(upToSection, upToLocation)</c>,
    /// each with its latest cached short summary at-or-before that position if any. Null
    /// if the book does not exist.</summary>
    public async Task<IReadOnlyList<StoryGuideEntityView>?> GetVisibleListAsync(
        Guid bookId, int upToSection, int upToLocation, CancellationToken ct)
    {
        if (!await db.Books.AnyAsync(b => b.Id == bookId, ct))
        {
            return null;
        }

        var entities = await db.BookEntities.AsNoTracking()
            .Where(e => e.BookId == bookId
                && e.FirstSeenSection != null
                && (e.FirstSeenSection < upToSection
                    || (e.FirstSeenSection == upToSection && (e.FirstSeenLocation ?? 0) <= upToLocation)))
            .OrderBy(e => e.FirstSeenSection)
            .ThenBy(e => e.CanonicalName)
            .Select(e => new
            {
                e.Id,
                e.CanonicalName,
                e.EntityType,
                e.Aliases,
                e.FirstSeenSection,
            })
            .ToListAsync(ct);

        // Latest cached summary per entity at-or-before the position. A handful of rows
        // per book, so grouping in memory is cheaper than a correlated subquery.
        var latestSummary = (await db.BookEntitySummaries.AsNoTracking()
                .Where(s => s.BookId == bookId
                    && (s.UpToSection < upToSection
                        || (s.UpToSection == upToSection && s.UpToLocation <= upToLocation)))
                .OrderByDescending(s => s.UpToSection)
                .ThenByDescending(s => s.UpToLocation)
                .Select(s => new { s.EntityId, s.ShortSummary })
                .ToListAsync(ct))
            .GroupBy(s => s.EntityId)
            .ToDictionary(g => g.Key, g => g.First().ShortSummary);

        return entities
            .Select(e => new StoryGuideEntityView
            {
                EntityId = e.Id,
                CanonicalName = e.CanonicalName,
                EntityType = e.EntityType.ToString(),
                Aliases = e.Aliases,
                FirstSeenSection = e.FirstSeenSection,
                ShortSummary = latestSummary.GetValueOrDefault(e.Id),
            })
            .ToList();
    }
}

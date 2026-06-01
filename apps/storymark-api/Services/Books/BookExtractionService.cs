using Microsoft.EntityFrameworkCore;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Services.Books;

/// <summary>Read access to the per-section extraction output (docs/plan.md §5.1
/// endpoints): events and relationships filtered to the reader's position. The same
/// spoiler boundary the retrieval path uses (section before current, or same section
/// up to the current location) applies here so a future-introduced event or
/// relationship is never returned.</summary>
internal sealed class BookExtractionService(StorymarkDbContext db)
{
    /// <summary>Events visible at <c>(upToSection, upToLocation)</c>, ordered by
    /// position, or <c>null</c> if the book does not exist.</summary>
    public async Task<IReadOnlyList<EventView>?> GetEventsAsync(
        Guid bookId, int upToSection, int upToLocation, CancellationToken ct)
    {
        if (!await db.Books.AnyAsync(b => b.Id == bookId, ct))
        {
            return null;
        }

        return await db.BookEvents.AsNoTracking()
            .Where(e => e.BookId == bookId
                && (e.SectionIndex < upToSection
                    || (e.SectionIndex == upToSection && e.LocationStart <= upToLocation)))
            .OrderBy(e => e.SectionIndex)
            .ThenBy(e => e.LocationStart)
            .Select(e => new EventView
            {
                SectionIndex = e.SectionIndex,
                LocationStart = e.LocationStart,
                LocationEnd = e.LocationEnd,
                EventType = e.EventType,
                Description = e.Description,
                InvolvedEntityIds = e.InvolvedEntityIds,
            })
            .ToListAsync(ct);
    }

    /// <summary>Relationships first seen at-or-before <c>(upToSection, upToLocation)</c>,
    /// ordered by first-seen position, or <c>null</c> if the book does not exist.</summary>
    public async Task<IReadOnlyList<RelationshipView>?> GetRelationshipsAsync(
        Guid bookId, int upToSection, int upToLocation, CancellationToken ct)
    {
        if (!await db.Books.AnyAsync(b => b.Id == bookId, ct))
        {
            return null;
        }

        return await db.BookRelationships.AsNoTracking()
            .Where(r => r.BookId == bookId
                && (r.FirstSeenSection < upToSection
                    || (r.FirstSeenSection == upToSection && r.FirstSeenLocation <= upToLocation)))
            .OrderBy(r => r.FirstSeenSection)
            .ThenBy(r => r.FirstSeenLocation)
            .Select(r => new RelationshipView
            {
                FromEntityId = r.FromEntityId,
                ToEntityId = r.ToEntityId,
                RelationshipType = r.RelationshipType,
                Polarity = r.Polarity,
                Evidence = r.Evidence,
                FirstSeenSection = r.FirstSeenSection,
                FirstSeenLocation = r.FirstSeenLocation,
            })
            .ToListAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Services.Entities;

/// <summary>Folds one or more loser <see cref="BookEntity"/> rows into a survivor when the
/// extractor split one real person/place/thing across several rows (e.g. "Kovacs",
/// "Mr. Kovacs", "Takeshi Kovacs"). Repoints every row that references a loser — mentions,
/// relationship endpoints, and event involvements — onto the survivor, folds the losers'
/// names into the survivor's aliases, pulls first-seen back to the earliest of the set
/// (so the merge can never reveal the entity earlier than the reader already met it), and
/// deletes the losers. Cached summaries for all involved rows are dropped so the next tap
/// regenerates one from the unified mention set. The book's <c>UpdatedAt</c> is bumped in the
/// same commit so the version-gated reader caches (section content carries the highlight
/// entity set, and the entity-derived surfaces) invalidate across every device — otherwise a
/// cached section would keep a deleted loser's id and 404 on tap. All changes commit in a
/// single <c>SaveChangesAsync</c>, so the merge is atomic. Drives both the manual Story-Guide
/// merge and the automatic alias reconciliation pass in <c>SectionExtractionJob</c>.</summary>
internal sealed class EntityMergeService(StorymarkDbContext db)
{
    /// <summary>Merges <paramref name="loserIds"/> into <paramref name="survivorId"/> within
    /// one book. Returns the updated survivor (with no short summary — summaries were just
    /// cleared), or <c>null</c> when the request is invalid: the survivor or any loser is
    /// unknown or belongs to another book, or nothing remains to merge once the survivor is
    /// removed from the loser set.</summary>
    public async Task<StoryGuideEntityView?> MergeAsync(
        Guid bookId, Guid survivorId, IReadOnlyList<Guid> loserIds, CancellationToken ct)
    {
        var distinctLosers = loserIds.Where(id => id != survivorId).Distinct().ToList();
        if (distinctLosers.Count == 0)
        {
            return null;
        }

        var survivor = await db.BookEntities
            .FirstOrDefaultAsync(e => e.Id == survivorId && e.BookId == bookId, ct);
        if (survivor is null)
        {
            return null;
        }

        var losers = await db.BookEntities
            .Where(e => e.BookId == bookId && distinctLosers.Contains(e.Id))
            .ToListAsync(ct);
        if (losers.Count != distinctLosers.Count)
        {
            return null;
        }

        var loserIdSet = losers.Select(l => l.Id).ToHashSet();

        await RepointMentionsAsync(bookId, survivorId, loserIdSet, ct);
        await RepointRelationshipsAsync(bookId, survivorId, loserIdSet, ct);
        await RewriteEventInvolvementAsync(bookId, survivorId, loserIdSet, ct);
        await DropSummariesAsync(bookId, survivorId, loserIdSet, ct);

        ApplyEntityMerge(survivor, losers);
        db.BookEntities.RemoveRange(losers);

        // Bump the book version in the same commit: section content embeds the visible entity
        // set for highlighting and is cached version-gated with no TTL, so without this the
        // reader keeps tapping a folded-away loser's id (a 404) until a reprocess. The survivor
        // is guaranteed to belong to this book, so the book exists.
        var book = await db.Books.FirstAsync(b => b.Id == bookId, ct);
        book.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return new StoryGuideEntityView
        {
            EntityId = survivor.Id,
            CanonicalName = survivor.CanonicalName,
            EntityType = survivor.EntityType.ToString(),
            Aliases = survivor.Aliases,
            FirstSeenSection = survivor.FirstSeenSection,
            ShortSummary = null,
        };
    }

    private async Task RepointMentionsAsync(
        Guid bookId, Guid survivorId, HashSet<Guid> loserIds, CancellationToken ct)
    {
        var mentions = await db.BookEntityMentions
            .Where(m => m.BookId == bookId && loserIds.Contains(m.EntityId))
            .ToListAsync(ct);
        foreach (var mention in mentions)
        {
            mention.EntityId = survivorId;
        }
    }

    // Repoint endpoints, then collapse only what the repoint made redundant — relationships
    // not touched by the merge are left exactly as they were. A repointed edge whose
    // endpoints are now the same entity (a self-loop, e.g. an edge that was Kovacs↔Takeshi)
    // is dropped; a repointed edge that now shares (from, to, type) with another folds to one,
    // keeping the earliest first-seen so the graph still reveals it at the right place and
    // extending last-seen to cover both.
    private async Task RepointRelationshipsAsync(
        Guid bookId, Guid survivorId, HashSet<Guid> loserIds, CancellationToken ct)
    {
        var relationships = await db.BookRelationships
            .Where(r => r.BookId == bookId)
            .ToListAsync(ct);

        var touched = new HashSet<Guid>();
        foreach (var rel in relationships)
        {
            var changed = false;
            if (loserIds.Contains(rel.FromEntityId))
            {
                rel.FromEntityId = survivorId;
                changed = true;
            }

            if (loserIds.Contains(rel.ToEntityId))
            {
                rel.ToEntityId = survivorId;
                changed = true;
            }

            if (changed)
            {
                touched.Add(rel.Id);
            }
        }

        var toRemove = new List<BookRelationship>();
        foreach (var rel in relationships.Where(r => touched.Contains(r.Id) && r.FromEntityId == r.ToEntityId))
        {
            toRemove.Add(rel);
        }

        // Only collapse a (from, to, type) group the merge actually collided into — i.e. one
        // that now contains a repointed edge. Pre-existing duplicates the merge didn't touch
        // stay untouched.
        var groups = relationships
            .Where(r => r.FromEntityId != r.ToEntityId && !toRemove.Contains(r))
            .GroupBy(r => (r.FromEntityId, r.ToEntityId, r.RelationshipType))
            .Where(g => g.Any(r => touched.Contains(r.Id)));
        foreach (var group in groups)
        {
            var ordered = group
                .OrderBy(r => r.FirstSeenSection)
                .ThenBy(r => r.FirstSeenLocation)
                .ToList();
            var keep = ordered[0];
            foreach (var duplicate in ordered.Skip(1))
            {
                ExtendLastSeen(keep, duplicate);
                toRemove.Add(duplicate);
            }
        }

        db.BookRelationships.RemoveRange(toRemove);
    }

    private static void ExtendLastSeen(BookRelationship keep, BookRelationship duplicate)
    {
        var keepLast = (keep.LastSeenSection ?? keep.FirstSeenSection,
            keep.LastSeenLocation ?? keep.FirstSeenLocation);
        var dupLast = (duplicate.LastSeenSection ?? duplicate.FirstSeenSection,
            duplicate.LastSeenLocation ?? duplicate.FirstSeenLocation);
        var latest = dupLast.CompareTo(keepLast) > 0 ? dupLast : keepLast;
        keep.LastSeenSection = latest.Item1;
        keep.LastSeenLocation = latest.Item2;
    }

    private async Task RewriteEventInvolvementAsync(
        Guid bookId, Guid survivorId, HashSet<Guid> loserIds, CancellationToken ct)
    {
        var events = await db.BookEvents
            .Where(e => e.BookId == bookId)
            .ToListAsync(ct);

        foreach (var bookEvent in events)
        {
            if (!bookEvent.InvolvedEntityIds.Any(loserIds.Contains))
            {
                continue;
            }

            var rewritten = new List<Guid>();
            foreach (var id in bookEvent.InvolvedEntityIds)
            {
                var mapped = loserIds.Contains(id) ? survivorId : id;
                if (!rewritten.Contains(mapped))
                {
                    rewritten.Add(mapped);
                }
            }

            bookEvent.InvolvedEntityIds = rewritten;
        }
    }

    private async Task DropSummariesAsync(
        Guid bookId, Guid survivorId, HashSet<Guid> loserIds, CancellationToken ct)
    {
        var summaries = await db.BookEntitySummaries
            .Where(s => s.BookId == bookId
                && (s.EntityId == survivorId || loserIds.Contains(s.EntityId)))
            .ToListAsync(ct);
        db.BookEntitySummaries.RemoveRange(summaries);
    }

    private static void ApplyEntityMerge(BookEntity survivor, IReadOnlyList<BookEntity> losers)
    {
        // Fold every loser's canonical name and aliases into the survivor's alias set, so the
        // shorter/honorific forms still resolve and show under the main name.
        var candidates = new List<string>(survivor.Aliases);
        foreach (var loser in losers)
        {
            candidates.Add(loser.CanonicalName);
            candidates.AddRange(loser.Aliases);
        }

        survivor.Aliases = EntityNameNormalizer.CleanAliasSet(candidates, survivor.CanonicalName);

        foreach (var loser in losers)
        {
            if (IsEarlier(loser, survivor))
            {
                survivor.FirstSeenSection = loser.FirstSeenSection;
                survivor.FirstSeenLocation = loser.FirstSeenLocation;
            }
        }

        if (survivor.EntityType == EntityType.Unknown)
        {
            var known = losers.FirstOrDefault(l => l.EntityType != EntityType.Unknown);
            if (known is not null)
            {
                survivor.EntityType = known.EntityType;
            }
        }

        survivor.UpdatedAt = DateTimeOffset.UtcNow;
    }

    // A loser was introduced earlier than the survivor (so the survivor must adopt its
    // position). An entity with no first-seen is treated as "never seen" and never wins.
    private static bool IsEarlier(BookEntity loser, BookEntity survivor)
    {
        if (loser.FirstSeenSection is not int loserSection)
        {
            return false;
        }

        if (survivor.FirstSeenSection is not int survivorSection)
        {
            return true;
        }

        return loserSection < survivorSection
            || (loserSection == survivorSection
                && (loser.FirstSeenLocation ?? 0) < (survivor.FirstSeenLocation ?? 0));
    }
}

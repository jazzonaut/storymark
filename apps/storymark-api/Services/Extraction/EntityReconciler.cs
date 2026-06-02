using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Services.Extraction;

/// <summary>Reconciles extracted entity names to <see cref="BookEntity"/> rows during
/// a section-extraction run (docs/adr/0009): a match on the separator-insensitive
/// identity key (<see cref="EntityNameNormalizer.MatchKey"/>) of a canonical name or
/// alias reuses the existing row, otherwise a new one is inserted. Keying on MatchKey
/// (not the display-form <see cref="EntityNameNormalizer.Normalize"/>) means spacing and
/// case variants the model emits across sections — "Mrs. Bancroft" / "Mrs.Bancroft" —
/// collapse to one entity, the same identity the reader's highlighter and the first-seen
/// backfill use. The stored canonical name keeps its original punctuation. Holds the
/// book's entities in memory keyed this way so reconciliation is consistent within a
/// single (sequential) run without a round-trip per name. New rows are added to the
/// tracked <see cref="StorymarkDbContext"/>; the caller saves.</summary>
internal sealed class EntityReconciler
{
    private readonly StorymarkDbContext _db;
    private readonly Guid _bookId;
    private readonly DateTimeOffset _now;
    private readonly Dictionary<string, BookEntity> _byKey;

    private EntityReconciler(
        StorymarkDbContext db, Guid bookId, DateTimeOffset now, Dictionary<string, BookEntity> byKey)
    {
        _db = db;
        _bookId = bookId;
        _now = now;
        _byKey = byKey;
    }

    public static async Task<EntityReconciler> CreateAsync(
        StorymarkDbContext db, Guid bookId, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await db.BookEntities.Where(e => e.BookId == bookId).ToListAsync(ct);
        var byKey = new Dictionary<string, BookEntity>(StringComparer.Ordinal);
        foreach (var entity in existing)
        {
            Index(byKey, entity);
        }

        return new EntityReconciler(db, bookId, now, byKey);
    }

    /// <summary>Resolves a name to an entity, reusing a match or inserting a new row.
    /// Returns <c>null</c> when the name normalises to nothing (so the caller drops the
    /// event involvement / relationship endpoint rather than inventing an entity).</summary>
    public BookEntity? Resolve(
        string? rawName,
        EntityType type,
        IReadOnlyList<string>? aliases,
        int sectionIndex,
        int? location)
    {
        if (rawName is null)
        {
            return null;
        }

        var core = EntityNameNormalizer.Normalize(rawName);
        if (core.Length == 0)
        {
            return null;
        }

        var key = EntityNameNormalizer.MatchKey(core);
        if (_byKey.TryGetValue(key, out var existing))
        {
            Enrich(existing, type, aliases);
            return existing;
        }

        var entity = new BookEntity
        {
            Id = Guid.NewGuid(),
            BookId = _bookId,
            CanonicalName = core,
            EntityType = type,
            Aliases = CleanAliases(aliases, core),
            FirstSeenSection = sectionIndex,
            FirstSeenLocation = location ?? 0,
            CreatedAt = _now,
            UpdatedAt = _now,
        };
        _db.BookEntities.Add(entity);
        Index(_byKey, entity);
        return entity;
    }

    /// <summary>Folds new information into an already-known entity: upgrades a
    /// placeholder <see cref="EntityType.Unknown"/> to a real type, and merges new
    /// aliases. First-seen position is left untouched — sections are processed in
    /// order, so the earliest sighting already won.</summary>
    private void Enrich(BookEntity entity, EntityType type, IReadOnlyList<string>? aliases)
    {
        var changed = false;

        if (entity.EntityType == EntityType.Unknown && type != EntityType.Unknown)
        {
            entity.EntityType = type;
            changed = true;
        }

        var merged = CleanAliases(aliases, entity.CanonicalName);
        if (merged.Count > 0)
        {
            var union = DedupeByKey(entity.Aliases.Concat(merged));
            if (union.Count != entity.Aliases.Count)
            {
                entity.Aliases = union;
                changed = true;
                foreach (var alias in merged)
                {
                    _byKey.TryAdd(EntityNameNormalizer.MatchKey(alias), entity);
                }
            }
        }

        if (changed)
        {
            entity.UpdatedAt = _now;
        }
    }

    private static List<string> CleanAliases(IReadOnlyList<string>? aliases, string canonical)
    {
        if (aliases is null || aliases.Count == 0)
        {
            return [];
        }

        var canonicalKey = EntityNameNormalizer.MatchKey(canonical);
        return DedupeByKey(aliases
            .Select(EntityNameNormalizer.Normalize)
            .Where(a => a.Length > 0
                && !string.Equals(EntityNameNormalizer.MatchKey(a), canonicalKey, StringComparison.Ordinal)));
    }

    // Dedupe display names by identity key (MatchKey), keeping the first form seen, so
    // spacing/case variants ("Mrs. Bancroft" / "Mrs.Bancroft") collapse to a single stored
    // alias — the same identity the reconciler and highlighter key on.
    private static List<string> DedupeByKey(IEnumerable<string> names) =>
        names.GroupBy(EntityNameNormalizer.MatchKey, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

    private static void Index(Dictionary<string, BookEntity> byKey, BookEntity entity)
    {
        byKey.TryAdd(EntityNameNormalizer.MatchKey(entity.CanonicalName), entity);
        foreach (var alias in entity.Aliases)
        {
            var key = EntityNameNormalizer.MatchKey(alias);
            if (key.Length > 0)
            {
                byKey.TryAdd(key, entity);
            }
        }
    }
}

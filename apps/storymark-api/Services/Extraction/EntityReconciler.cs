using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Services.Extraction;

/// <summary>Reconciles extracted entity names to <see cref="BookEntity"/> rows during
/// a section-extraction run (docs/adr/0009): a normalised canonical-name/alias match
/// reuses the existing row, otherwise a new one is inserted. Holds the book's entities
/// in memory keyed by normalised name so reconciliation is consistent within a single
/// (sequential) run without a round-trip per name. New rows are added to the tracked
/// <see cref="StorymarkDbContext"/>; the caller saves.</summary>
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

        var key = core.ToLowerInvariant();
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
            var union = entity.Aliases
                .Concat(merged)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (union.Count != entity.Aliases.Count)
            {
                entity.Aliases = union;
                changed = true;
                foreach (var alias in merged)
                {
                    var aliasKey = EntityNameNormalizer.Normalize(alias).ToLowerInvariant();
                    _byKey.TryAdd(aliasKey, entity);
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

        return aliases
            .Select(EntityNameNormalizer.Normalize)
            .Where(a => a.Length > 0 && !string.Equals(a, canonical, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void Index(Dictionary<string, BookEntity> byKey, BookEntity entity)
    {
        byKey.TryAdd(EntityNameNormalizer.Normalize(entity.CanonicalName).ToLowerInvariant(), entity);
        foreach (var alias in entity.Aliases)
        {
            var key = EntityNameNormalizer.Normalize(alias).ToLowerInvariant();
            if (key.Length > 0)
            {
                byKey.TryAdd(key, entity);
            }
        }
    }
}

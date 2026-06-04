using System.Text.RegularExpressions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Llm;
using Storymark.Api.Infrastructure.Storage;
using Storymark.Api.Infrastructure.Text;
using Storymark.Api.Services.Entities;
using Storymark.Api.Services.Extraction;
using Storymark.Api.Services.Llm;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Jobs;

/// <summary>Per-book structured extraction (docs/plan.md §15.2, ADR-0009). For each
/// section, in reading order, it makes a single <c>task:"extract"</c> LLM call that
/// returns one JSON object (summary + events + relationship deltas + entities
/// introduced), then decomposes it at the persistence seam into
/// <see cref="SectionSummary"/>/<see cref="BookEvent"/>/<see cref="BookRelationship"/>
/// rows, reconciling entity names to <see cref="BookEntity"/> rows via
/// <see cref="EntityReconciler"/>.
///
/// <para>Sequential rather than one Hangfire job per section: entity reconciliation
/// does match-or-insert against <c>unique(book_id, canonical_name)</c>, which parallel
/// jobs would race. One call per section still holds (ADR-0009).</para>
///
/// <para>This is the <c>ExtractingPerSection</c> step of the pipeline; on completion
/// it advances the book to <c>BuildingMemorySnapshots</c> and enqueues
/// <see cref="BookMemorySnapshotJob"/>, which composes the rolling memory and then
/// chains to <see cref="EmbeddingJob"/> — the unchanged terminal step that finishes at
/// <c>Indexed</c> (so an S3.4 reindex, which only re-runs <see cref="EmbeddingJob"/>,
/// never re-extracts). Idempotent: a re-run clears this book's prior extraction
/// first.</para>
///
/// <para>Tolerant per ADR-0009: an unparseable section retries once with a stricter
/// prompt, then is skipped (logged) with partial data preserved. A transient call
/// failure on a single section is also logged and skipped so one flaky section can't
/// abandon the whole book. A missing extract model (<see cref="ModelNotFoundException"/>)
/// is a deterministic config error — it fails the book to <c>Failed</c>.</para></summary>
internal sealed class SectionExtractionJob(
    StorymarkDbContext db,
    FileStorageService storage,
    ChatModelRouter router,
    ISettingsService settings,
    IBackgroundJobClient jobs,
    EntityMergeService entityMerge,
    ILogger<SectionExtractionJob> logger)
{
    private const int SurroundingMaxChars = 160;
    private const int MinTermLength = 2;
    private static readonly TimeSpan FirstSeenScanTimeout = TimeSpan.FromSeconds(5);
    private static readonly Regex TermSeparators = new(@"[.\s]+", RegexOptions.CultureInvariant, FirstSeenScanTimeout);

    public async Task RunAsync(Guid bookId, CancellationToken ct)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == bookId, ct);
        if (book is null)
        {
            logger.LogWarning("SectionExtractionJob skipped: book {BookId} not found", bookId);
            return;
        }

        try
        {
            if (book.ProcessingStatus != ProcessingStatus.ExtractingPerSection)
            {
                await SetStatusAsync(book, ProcessingStatus.ExtractingPerSection, ct);
            }

            await ClearExistingExtractionAsync(bookId, ct);
            await ExtractAllAsync(book, ct);
            await TryBackfillFirstSeenAsync(book, ct);
            await TryReconcileAliasesAsync(book, ct);

            // Hand off to the memory pass (BuildingMemorySnapshots), which then chains
            // to EmbeddingJob → Indexed. Memory needs only the section summaries just
            // written, not vectors, so it runs before embedding (ADR-0018).
            await SetStatusAsync(book, ProcessingStatus.BuildingMemorySnapshots, ct);
            jobs.Enqueue<BookMemorySnapshotJob>(j => j.RunAsync(book.Id, CancellationToken.None));
        }
        catch (ModelNotFoundException ex)
        {
            // Deterministic config error (the extract model isn't pulled): every section
            // would fail identically, so fail the book fast with the reason rather than
            // retrying. Requeue after pulling the model.
            logger.LogError(ex, "SectionExtractionJob failed for book {BookId}: model unavailable", bookId);
            book.ProcessingError = ex.Message;
            await SetStatusAsync(book, ProcessingStatus.Failed, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Per-section transient failures are already swallowed inside the loop, so an
            // exception escaping here is a setup/persistence error. Land the book on Failed
            // with the reason rather than letting Hangfire retry into a permanent
            // intermediate ("ExtractingPerSection") status with nothing to act on.
            logger.LogError(ex, "SectionExtractionJob failed for book {BookId}: unexpected error", bookId);
            book.ProcessingError = ex.Message;
            await SetStatusAsync(book, ProcessingStatus.Failed, CancellationToken.None);
        }
    }

    private async Task ExtractAllAsync(Book book, CancellationToken ct)
    {
        var sections = await db.BookSections
            .Where(s => s.BookId == book.Id)
            .OrderBy(s => s.SectionIndex)
            .ToListAsync(ct);

        var instruction = settings.GetString("prompts.sectionExtraction");
        var reconciler = await EntityReconciler.CreateAsync(db, book.Id, DateTimeOffset.UtcNow, ct);

        var total = new SectionCounts(0, 0, 0);
        var skipped = 0;

        foreach (var section in sections)
        {
            var counts = await ProcessSectionAsync(book, section, instruction, reconciler, ct);
            if (counts is null)
            {
                skipped++;
                continue;
            }

            total = total.Add(counts);
        }

        logger.LogInformation(
            "Extracted book {BookId}: {Summaries} summaries, {Events} events, {Relationships} relationships ({Skipped} sections skipped)",
            book.Id, total.Summaries, total.Events, total.Relationships, skipped);
    }

    /// <summary>Runs the first-seen backfill as a best-effort enhancement over
    /// already-persisted extraction: a failure here (e.g. a scan timeout) must not fail an
    /// otherwise-successful book, so it's swallowed and the model-derived positions stand.</summary>
    private async Task TryBackfillFirstSeenAsync(Book book, CancellationToken ct)
    {
        try
        {
            await BackfillFirstSeenAsync(book, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "First-seen backfill failed for book {BookId}; skipping", book.Id);
        }
    }

    /// <summary>Folds entities the model itself split apart: when one entity's canonical name
    /// is a strong alias of exactly one other entity, the two name the same person/place/thing
    /// (e.g. a bare "Kovacs" row and a "Takeshi Kovacs" row that lists "Kovacs" as an alias),
    /// so the bare row is merged into the fuller one (<see cref="EntityMergeService"/>). Runs
    /// after the first-seen backfill so the survivor adopts the earliest already-resolved
    /// position. Best-effort, like the backfill: a failure must not fail an otherwise-successful
    /// book, so it's swallowed.
    ///
    /// <para>Conservative on purpose: an entity claimed as an alias by more than one other (an
    /// ambiguous bare surname two characters share) is never folded automatically — it's left
    /// for a human to merge, so this pass can't collapse two distinct characters.</para></summary>
    private async Task TryReconcileAliasesAsync(Book book, CancellationToken ct)
    {
        try
        {
            await ReconcileAliasesAsync(book, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Alias reconciliation failed for book {BookId}; skipping", book.Id);
        }
    }

    private async Task ReconcileAliasesAsync(Book book, CancellationToken ct)
    {
        var entities = await db.BookEntities.Where(e => e.BookId == book.Id).ToListAsync(ct);

        var merged = 0;
        foreach (var group in FindAliasMerges(entities).GroupBy(p => p.SurvivorId))
        {
            var losers = group.Select(p => p.LoserId).ToList();
            if (await entityMerge.MergeUnscopedAsync(book.Id, group.Key, losers, ct) is not null)
            {
                merged += losers.Count;
            }
        }

        if (merged > 0)
        {
            logger.LogInformation(
                "Alias reconciliation merged {Count} duplicate entities in book {BookId}", merged, book.Id);
        }
    }

    /// <summary>Unambiguous (loser → survivor) merges from the model's own alias declarations:
    /// a loser whose canonical name is a strong alias of exactly one other entity of a
    /// compatible type. Entities involved in an alias chain (both a survivor and a loser) are
    /// excluded — merging them in one pass would be order-dependent; they resolve on a later
    /// reprocess or a manual merge.</summary>
    private static List<(Guid LoserId, Guid SurvivorId)> FindAliasMerges(IReadOnlyList<BookEntity> entities)
    {
        var byCanonicalKey = new Dictionary<string, BookEntity>(StringComparer.Ordinal);
        foreach (var entity in entities)
        {
            byCanonicalKey.TryAdd(EntityNameNormalizer.MatchKey(entity.CanonicalName), entity);
        }

        var survivorsByLoser = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var survivor in entities)
        {
            foreach (var alias in survivor.Aliases)
            {
                if (EntityAliasRules.IsStrongAlias(alias)
                    && byCanonicalKey.TryGetValue(EntityNameNormalizer.MatchKey(alias), out var loser)
                    && loser.Id != survivor.Id
                    && AreTypesCompatible(survivor.EntityType, loser.EntityType))
                {
                    if (!survivorsByLoser.TryGetValue(loser.Id, out var claimants))
                    {
                        claimants = [];
                        survivorsByLoser[loser.Id] = claimants;
                    }

                    claimants.Add(survivor.Id);
                }
            }
        }

        var pairs = survivorsByLoser
            .Where(kv => kv.Value.Count == 1)
            .Select(kv => (LoserId: kv.Key, SurvivorId: kv.Value.Single()))
            .ToList();

        var survivorIds = pairs.Select(p => p.SurvivorId).ToHashSet();
        var loserIds = pairs.Select(p => p.LoserId).ToHashSet();
        return pairs
            .Where(p => !loserIds.Contains(p.SurvivorId) && !survivorIds.Contains(p.LoserId))
            .ToList();
    }

    // An auto-merge fires only when the two rows agree on type, or one is still Unknown so the
    // merge can resolve it. A coincidental name collision across types (a person and a place
    // named after them) must never fold automatically — the manual merge stays unrestricted.
    private static bool AreTypesCompatible(EntityType a, EntityType b) =>
        a == b || a == EntityType.Unknown || b == EntityType.Unknown;

    /// <summary>Pulls each entity's first-seen position back to where its name first
    /// occurs in the prose, not where the model first named it in structured output. The
    /// extractor only stamps first-seen when an entity surfaces in <c>entitiesIntroduced</c>
    /// / an event / a relationship, which can lag its true first mention by many sections
    /// (e.g. a place named in the prologue but not modelled until much later) — leaving it
    /// hidden from the Story Guide and unhighlighted where it actually appears. The scan
    /// uses the same folded, separator-flexible whole-word matching as the reader's
    /// highlighter (highlight-entities.ts), so first-seen lands where the name first
    /// highlights. Matches only the entity's own canonical name and strong aliases against
    /// real occurrences in the prose, so it can only reveal an entity on a page where its
    /// name actually appears — never earlier, and never from a generic role word.</summary>
    private async Task BackfillFirstSeenAsync(Book book, CancellationToken ct)
    {
        var entities = await db.BookEntities.Where(e => e.BookId == book.Id).ToListAsync(ct);
        if (entities.Count == 0)
        {
            return;
        }

        var index = BuildTermIndex(entities);
        if (index is null)
        {
            return;
        }

        var earliest = await ScanEarliestOccurrencesAsync(book.Id, index.Value, entities.Count, ct);
        if (ApplyEarliest(entities, earliest))
        {
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Maps each entity's separator-insensitive match key (<see
    /// cref="EntityNameNormalizer.MatchKey"/>) to the entity, plus a whole-word alternation
    /// over the folded terms (longest-first, so a multi-word name wins over one of its
    /// parts). Each term's internal separators flex (<c>[.\s]+</c>) so "Mrs. Bancroft" also
    /// matches the prose's "Mrs.Bancroft" — the same matching the reader's highlighter uses
    /// (highlight-entities.ts). The canonical name is always indexed; aliases only when they
    /// are strong (<see cref="EntityAliasRules.IsStrongAlias"/>), so a generic role word the model tagged as
    /// an alias ("the doctor", "captain") can't drag first-seen onto an unrelated occurrence.
    /// Each term carries whether it's a proper noun (<see cref="TermHit.RequiresCapital"/>) so
    /// the scan can demand a capitalised occurrence for "Rose" but match a lowercase
    /// common-noun name ("the ring") as-is. Returns <c>null</c> when no term survives.</summary>
    private static (Dictionary<string, TermHit> ByTerm, Regex Pattern)? BuildTermIndex(
        IReadOnlyList<BookEntity> entities)
    {
        var byTerm = new Dictionary<string, TermHit>(StringComparer.Ordinal);
        var terms = new List<string>();

        void AddTerm(string raw, BookEntity entity)
        {
            var key = EntityNameNormalizer.MatchKey(raw);
            var folded = EntityNameNormalizer.Fold(raw);
            if (key.Length >= MinTermLength && byTerm.TryAdd(key, new TermHit(entity, folded.Any(char.IsUpper))))
            {
                terms.Add(folded.Trim());
            }
        }

        foreach (var entity in entities)
        {
            AddTerm(entity.CanonicalName, entity);
            foreach (var alias in entity.Aliases)
            {
                if (EntityAliasRules.IsStrongAlias(alias))
                {
                    AddTerm(alias, entity);
                }
            }
        }

        if (terms.Count == 0)
        {
            return null;
        }

        terms.Sort((a, b) => b.Length - a.Length);
        var pattern = new Regex(
            @"\b(?:" + string.Join("|", terms.Select(TermPattern)) + @")\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            FirstSeenScanTimeout);
        return (byTerm, pattern);
    }

    /// <summary>A regex source for a folded term whose internal separators flex: parts are
    /// split on whitespace/abbreviation periods and rejoined with <c>[.\s]+</c>, so the
    /// stored name matches the book's spacing variants. Mirrors <c>termPattern</c> in
    /// highlight-entities.ts. Parts stay anchored, so a name with the separator dropped
    /// entirely ("MrsBancroft") still won't match.</summary>
    private static string TermPattern(string foldedTerm) =>
        string.Join(
            @"[.\s]+",
            TermSeparators.Split(foldedTerm).Where(p => p.Length > 0).Select(Regex.Escape));

    private async Task<Dictionary<Guid, (int Section, int Location)>> ScanEarliestOccurrencesAsync(
        Guid bookId, (Dictionary<string, TermHit> ByTerm, Regex Pattern) index, int entityCount, CancellationToken ct)
    {
        var sections = await db.BookSections
            .Where(s => s.BookId == bookId)
            .OrderBy(s => s.SectionIndex)
            .ToListAsync(ct);

        var earliest = new Dictionary<Guid, (int Section, int Location)>();
        foreach (var section in sections)
        {
            if (earliest.Count == entityCount)
            {
                break;
            }

            var text = section.TextPath is null ? null : await storage.ReadTextAsync(section.TextPath, ct);
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            // Fold is one char per char, so a match index in the folded haystack is the same
            // character offset in the original text the locations are measured against. Within
            // a section matches arrive in order, so the first TryAdd per entity wins.
            foreach (Match match in index.Pattern.Matches(EntityNameNormalizer.Fold(text)))
            {
                // For a proper-noun term ("Rose") a lowercase occurrence ("the rose") is a
                // common word, not the entity — anchoring first-seen there would reveal it at
                // an unrelated earlier mention, so skip it. A lowercase common-noun name (an
                // Object/Concept like "the ring") has no such collision and matches as-is. The
                // reader's highlighter applies the same per-term test (highlight-entities.ts)
                // so highlight and gating agree.
                if (index.ByTerm.TryGetValue(EntityNameNormalizer.MatchKey(match.Value), out var hit)
                    && (!hit.RequiresCapital || match.Value.Any(char.IsUpper)))
                {
                    earliest.TryAdd(hit.Entity.Id, (section.SectionIndex, match.Index));
                }
            }
        }

        return earliest;
    }

    private static bool ApplyEarliest(
        IReadOnlyList<BookEntity> entities, Dictionary<Guid, (int Section, int Location)> earliest)
    {
        var changed = false;
        foreach (var entity in entities)
        {
            if (!earliest.TryGetValue(entity.Id, out var pos))
            {
                continue;
            }

            var isEarlier = entity.FirstSeenSection is not int seen
                || pos.Section < seen
                || (pos.Section == seen && pos.Location < (entity.FirstSeenLocation ?? 0));
            if (isEarlier)
            {
                entity.FirstSeenSection = pos.Section;
                entity.FirstSeenLocation = pos.Location;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>Extracts and persists one section, or returns <c>null</c> if it was
    /// skipped (blank text, unparseable after retry, or a transient call failure).</summary>
    private async Task<SectionCounts?> ProcessSectionAsync(
        Book book, BookSection section, string instruction, EntityReconciler reconciler, CancellationToken ct)
    {
        var text = section.TextPath is null ? null : await storage.ReadTextAsync(section.TextPath, ct);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        SectionExtraction? extraction;
        try
        {
            extraction = await ExtractSectionAsync(book, section, instruction, text, ct);
        }
        catch (ModelNotFoundException)
        {
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Best-effort: a single section's call failing (timeout, provider 5xx)
            // shouldn't abandon the book — extraction is an enhancement layer and
            // embedding/Q&A still run afterwards.
            logger.LogWarning(
                ex, "SectionExtractionJob: section {Index} of book {BookId} failed; skipping",
                section.SectionIndex, book.Id);
            return null;
        }

        if (extraction is null)
        {
            return null;
        }

        return await PersistSectionAsync(book.Id, section, text, extraction, reconciler, ct);
    }

    private async Task<SectionExtraction?> ExtractSectionAsync(
        Book book, BookSection section, string instruction, string text, CancellationToken ct)
    {
        var first = await CompleteAsync(book, section, instruction, text, strict: false, ct);
        var parsed = LooseJsonParser.Parse<SectionExtraction>(first);
        if (parsed is not null)
        {
            return parsed;
        }

        logger.LogWarning(
            "SectionExtractionJob: section {Index} of book {BookId} returned unparseable JSON; retrying strict",
            section.SectionIndex, book.Id);

        var second = await CompleteAsync(book, section, instruction, text, strict: true, ct);
        parsed = LooseJsonParser.Parse<SectionExtraction>(second);
        if (parsed is null)
        {
            logger.LogWarning(
                "SectionExtractionJob: section {Index} of book {BookId} still unparseable; skipping",
                section.SectionIndex, book.Id);
        }

        return parsed;
    }

    private async Task<string> CompleteAsync(
        Book book, BookSection section, string instruction, string text, bool strict, CancellationToken ct)
    {
        var prompt = strict
            ? instruction
                + "\n\nIMPORTANT: Respond with a single valid JSON object only — no markdown fences, no commentary before or after the JSON."
                + "\n\nSECTION TEXT:\n" + text
            : instruction + "\n\nSECTION TEXT:\n" + text;

        var messages = new[] { new ChatMessage { Role = ChatRole.User, Content = prompt } };
        var completion = await router.CompleteAsync(
            "extract",
            messages,
            new LlmCallMetadata { BookId = book.Id, SectionIndex = section.SectionIndex },
            ct);

        return completion.Content;
    }

    private async Task<SectionCounts> PersistSectionAsync(
        Guid bookId,
        BookSection section,
        string text,
        SectionExtraction extraction,
        EntityReconciler reconciler,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var entitiesSeen = new List<string>();

        // Fold once: every quote/name lookup below matches against the folded text so
        // straight-vs-curly punctuation never costs a match. Fold is length-preserving, so
        // an offset into the folded text is the same offset into the original the reader sees.
        var foldedText = EntityNameNormalizer.Fold(text);

        ResolveIntroduced(extraction, section, foldedText, reconciler, entitiesSeen);
        var (eventCount, summaryEvents) =
            PersistEvents(bookId, section, foldedText, extraction, reconciler, entitiesSeen, now);
        var relationshipCount = PersistRelationships(bookId, section, foldedText, extraction, reconciler, entitiesSeen, now);

        db.SectionSummaries.Add(new SectionSummary
        {
            Id = Guid.NewGuid(),
            BookId = bookId,
            SectionIndex = section.SectionIndex,
            Summary = (extraction.ShortSummary ?? string.Empty).Trim(),
            ImportantEvents = summaryEvents,
            EntitiesSeen = entitiesSeen.Distinct(StringComparer.Ordinal).ToList(),
            OpenThreads = CleanThreads(extraction.OpenThreads),
            GeneratedAt = now,
        });

        await db.SaveChangesAsync(ct);
        return new SectionCounts(1, eventCount, relationshipCount);
    }

    private static void ResolveIntroduced(
        SectionExtraction extraction, BookSection section, string foldedText, EntityReconciler reconciler, List<string> entitiesSeen)
    {
        foreach (var introduced in extraction.EntitiesIntroduced ?? [])
        {
            var entity = reconciler.Resolve(
                introduced.CanonicalName,
                ParseEntityType(introduced.EntityType),
                introduced.Aliases,
                section.SectionIndex,
                location: LocateEntity(foldedText, introduced));
            if (entity is not null)
            {
                entitiesSeen.Add(entity.CanonicalName);
            }
        }
    }

    private (int Count, List<SectionSummaryEvent> Summary) PersistEvents(
        Guid bookId,
        BookSection section,
        string foldedText,
        SectionExtraction extraction,
        EntityReconciler reconciler,
        List<string> entitiesSeen,
        DateTimeOffset now)
    {
        var summaryEvents = new List<SectionSummaryEvent>();
        var count = 0;

        foreach (var ev in extraction.ImportantEvents ?? [])
        {
            var description = (ev.Description ?? string.Empty).Trim();
            if (description.Length == 0)
            {
                continue;
            }

            var (locationStart, locationEnd) = LocateEvent(foldedText, ev);
            var eventType = CleanOptional(ev.EventType);
            var (involvedIds, involvedNames) = ResolveInvolved(
                bookId, section, locationStart, locationEnd, description, ev.InvolvedEntityNames, reconciler, entitiesSeen);

            db.BookEvents.Add(new BookEvent
            {
                Id = Guid.NewGuid(),
                BookId = bookId,
                SectionIndex = section.SectionIndex,
                LocationStart = locationStart,
                LocationEnd = locationEnd,
                EventType = eventType,
                Description = description,
                InvolvedEntityIds = involvedIds,
                CreatedAt = now,
            });
            summaryEvents.Add(new SectionSummaryEvent
            {
                Description = description,
                EventType = eventType,
                LocationStart = locationStart,
                LocationEnd = locationEnd,
                InvolvedEntityNames = involvedNames,
            });
            count++;
        }

        return (count, summaryEvents);
    }

    /// <summary>Resolves an event's involved-entity names to ids (de-duped), recording
    /// a located mention for each and appending to the section's seen set.</summary>
    private (List<Guid> Ids, List<string> Names) ResolveInvolved(
        Guid bookId,
        BookSection section,
        int locationStart,
        int locationEnd,
        string description,
        IReadOnlyList<string>? names,
        EntityReconciler reconciler,
        List<string> entitiesSeen)
    {
        var ids = new List<Guid>();
        var resolvedNames = new List<string>();

        foreach (var name in names ?? [])
        {
            var entity = reconciler.Resolve(name, EntityType.Unknown, null, section.SectionIndex, locationStart);
            if (entity is null || ids.Contains(entity.Id))
            {
                continue;
            }

            ids.Add(entity.Id);
            resolvedNames.Add(entity.CanonicalName);
            entitiesSeen.Add(entity.CanonicalName);
            db.BookEntityMentions.Add(new BookEntityMention
            {
                Id = Guid.NewGuid(),
                BookId = bookId,
                EntityId = entity.Id,
                SectionIndex = section.SectionIndex,
                LocationStart = locationStart,
                LocationEnd = locationEnd,
                MentionText = entity.CanonicalName,
                SurroundingText = Ellipsis.Truncate(description, SurroundingMaxChars),
                Confidence = 1.0m,
            });
        }

        return (ids, resolvedNames);
    }

    private int PersistRelationships(
        Guid bookId,
        BookSection section,
        string foldedText,
        SectionExtraction extraction,
        EntityReconciler reconciler,
        List<string> entitiesSeen,
        DateTimeOffset now)
    {
        var count = 0;

        foreach (var rel in extraction.RelationshipDeltas ?? [])
        {
            var location = LocateRelationship(foldedText, rel);
            var from = reconciler.Resolve(rel.FromName, EntityType.Unknown, null, section.SectionIndex, location);
            var to = reconciler.Resolve(rel.ToName, EntityType.Unknown, null, section.SectionIndex, location);
            if (from is null || to is null || from.Id == to.Id)
            {
                continue;
            }

            entitiesSeen.Add(from.CanonicalName);
            entitiesSeen.Add(to.CanonicalName);
            db.BookRelationships.Add(new BookRelationship
            {
                Id = Guid.NewGuid(),
                BookId = bookId,
                FromEntityId = from.Id,
                ToEntityId = to.Id,
                RelationshipType = CleanOptional(rel.Type) ?? "other",
                Polarity = CleanOptional(rel.Polarity),
                Evidence = CleanOptional(rel.Evidence),
                FirstSeenSection = section.SectionIndex,
                FirstSeenLocation = location,
                CreatedAt = now,
            });
            count++;
        }

        return count;
    }

    private async Task ClearExistingExtractionAsync(Guid bookId, CancellationToken ct)
    {
        // Deleting entities cascades to mentions and relationships; summaries and
        // events FK only to the book, so clear them explicitly. Keeps a re-run
        // (reprocess) idempotent — extraction owns all of these rows.
        await db.BookEntities.Where(e => e.BookId == bookId).ExecuteDeleteAsync(ct);
        await db.BookEvents.Where(e => e.BookId == bookId).ExecuteDeleteAsync(ct);
        await db.SectionSummaries.Where(s => s.BookId == bookId).ExecuteDeleteAsync(ct);
    }

    private async Task SetStatusAsync(Book book, ProcessingStatus status, CancellationToken ct)
    {
        book.ProcessingStatus = status;
        if (status != ProcessingStatus.Failed)
        {
            book.ProcessingError = null;
        }

        book.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static List<string> CleanThreads(IReadOnlyList<string>? threads) =>
        (threads ?? [])
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToList();

    private static EntityType ParseEntityType(string? value) =>
        Enum.TryParse<EntityType>(value, ignoreCase: true, out var type) ? type : EntityType.Unknown;

    /// <summary>The span of an event in the section, located by its verbatim evidence
    /// quote (small models can't produce reliable character offsets, so we don't ask for
    /// them). The prompt asks for a short quote, not a unique one, so the same phrase can
    /// recur; we take the <em>last</em> occurrence. The event is at one of the matches, all
    /// at or before the last, so gating on the last reveals only after the reader has passed
    /// the real one — never before (spoiler-safety invariant). An event whose quote can't be
    /// found is anchored to the section end, staying hidden until the reader finishes the
    /// section rather than surfacing at offset 0.</summary>
    private static (int Start, int End) LocateEvent(string foldedText, ExtractedEvent ev)
    {
        var quote = ev.Evidence?.Trim();
        var at = IndexOfOrdinal(foldedText, quote, last: true);
        return at >= 0 && quote is not null
            ? (at, Math.Min(at + quote.Length, foldedText.Length))
            : (foldedText.Length, foldedText.Length);
    }

    /// <summary>Best-effort offset of where an introduced entity is first seen in the
    /// section, so the Story Guide and graph gate it to the reader's location rather than
    /// surfacing it from the section start. Prefers the model's evidence quote, then the
    /// canonical name, then an alias; 0 only when none is found in the text.</summary>
    private static int LocateEntity(string foldedText, ExtractedEntity entity)
    {
        var at = IndexOfOrdinal(foldedText, entity.FirstSeenEvidence);
        if (at >= 0)
        {
            return at;
        }

        at = IndexOfOrdinal(foldedText, entity.CanonicalName);
        if (at >= 0)
        {
            return at;
        }

        foreach (var alias in entity.Aliases ?? [])
        {
            var aliasAt = IndexOfOrdinal(foldedText, alias);
            if (aliasAt >= 0)
            {
                return aliasAt;
            }
        }

        return 0;
    }

    /// <summary>Best-effort offset of where a relationship is first evidenced. Prefers the
    /// model's evidence quote; failing that, the point where both endpoints are first on
    /// the page together (the later of their two first mentions); 0 only when neither is
    /// found. Keeps a relationship introduced mid-section hidden until the reader reaches
    /// it rather than from the section start.</summary>
    private static int LocateRelationship(string foldedText, ExtractedRelationship rel)
    {
        var at = IndexOfOrdinal(foldedText, rel.Evidence);
        if (at >= 0)
        {
            return at;
        }

        var from = IndexOfOrdinal(foldedText, rel.FromName);
        var to = IndexOfOrdinal(foldedText, rel.ToName);
        return from >= 0 && to >= 0 ? Math.Max(from, to) : 0;
    }

    /// <summary>Index of <paramref name="needle"/> in already-folded section text, the first
    /// occurrence by default or the last when <paramref name="last"/> is set. The needle is
    /// folded too (it comes straight from the model/entity), so a quote with straight quotes
    /// matches prose with typographic ones. Fold is length-preserving, so the returned offset
    /// is valid against the original text.</summary>
    private static int IndexOfOrdinal(string foldedText, string? needle, bool last = false)
    {
        var trimmed = needle?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return -1;
        }

        var folded = EntityNameNormalizer.Fold(trimmed);
        return last
            ? foldedText.LastIndexOf(folded, StringComparison.OrdinalIgnoreCase)
            : foldedText.IndexOf(folded, StringComparison.OrdinalIgnoreCase);
    }

    private static string? CleanOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private sealed record SectionCounts(int Summaries, int Events, int Relationships)
    {
        public SectionCounts Add(SectionCounts other) =>
            new(Summaries + other.Summaries, Events + other.Events, Relationships + other.Relationships);
    }

    // A first-seen scan term: the entity it resolves to, plus whether it's a proper noun
    // (carries a capital) and so should only match capitalised occurrences in prose.
    private readonly record struct TermHit(BookEntity Entity, bool RequiresCapital);
}

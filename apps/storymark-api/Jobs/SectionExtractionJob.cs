using Hangfire;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Llm;
using Storymark.Api.Infrastructure.Storage;
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
    ILogger<SectionExtractionJob> logger)
{
    private const int SurroundingMaxChars = 160;

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

        return await PersistSectionAsync(book.Id, section, text.Length, extraction, reconciler, ct);
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
        int textLength,
        SectionExtraction extraction,
        EntityReconciler reconciler,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var entitiesSeen = new List<string>();

        ResolveIntroduced(extraction, section, reconciler, entitiesSeen);
        var (eventCount, summaryEvents) =
            PersistEvents(bookId, section, textLength, extraction, reconciler, entitiesSeen, now);
        var relationshipCount = PersistRelationships(bookId, section, extraction, reconciler, entitiesSeen, now);

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
        SectionExtraction extraction, BookSection section, EntityReconciler reconciler, List<string> entitiesSeen)
    {
        foreach (var introduced in extraction.EntitiesIntroduced ?? [])
        {
            var entity = reconciler.Resolve(
                introduced.CanonicalName,
                ParseEntityType(introduced.EntityType),
                introduced.Aliases,
                section.SectionIndex,
                location: null);
            if (entity is not null)
            {
                entitiesSeen.Add(entity.CanonicalName);
            }
        }
    }

    private (int Count, List<SectionSummaryEvent> Summary) PersistEvents(
        Guid bookId,
        BookSection section,
        int textLength,
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

            var (locationStart, locationEnd) = ClampRange(ev.LocationStart, ev.LocationEnd, textLength);
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
                SurroundingText = Truncate(description),
                Confidence = 1.0m,
            });
        }

        return (ids, resolvedNames);
    }

    private int PersistRelationships(
        Guid bookId,
        BookSection section,
        SectionExtraction extraction,
        EntityReconciler reconciler,
        List<string> entitiesSeen,
        DateTimeOffset now)
    {
        var count = 0;

        foreach (var rel in extraction.RelationshipDeltas ?? [])
        {
            var from = reconciler.Resolve(rel.FromName, EntityType.Unknown, null, section.SectionIndex, location: 0);
            var to = reconciler.Resolve(rel.ToName, EntityType.Unknown, null, section.SectionIndex, location: 0);
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
                FirstSeenLocation = 0,
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

    private static (int Start, int End) ClampRange(int start, int end, int textLength)
    {
        var s = Math.Clamp(start, 0, textLength);
        var e = Math.Clamp(end, 0, textLength);
        return e < s ? (s, s) : (s, e);
    }

    private static string? CleanOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string Truncate(string text) =>
        text.Length <= SurroundingMaxChars ? text : text[..SurroundingMaxChars].TrimEnd() + "…";

    private sealed record SectionCounts(int Summaries, int Events, int Relationships)
    {
        public SectionCounts Add(SectionCounts other) =>
            new(Summaries + other.Summaries, Events + other.Events, Relationships + other.Relationships);
    }
}
